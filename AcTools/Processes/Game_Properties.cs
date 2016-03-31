using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.Processes {
    public partial class Game {
        public enum SessionType {
            [Description("Booking")]
            Booking = 0,

            [Description("Practice")]
            Practice = 1,

            [Description("Qualification")]
            Qualification = 2,

            [Description("Race")]
            Race = 3,

            [Description("Hotlap")]
            Hotlap = 4,

            [Description("Time Attack")]
            TimeAttack = 5,

            [Description("Drift")]
            Drift = 6
        }

        public class StartType {
            public string Value { get; }
            public string Name { get; }

            public static readonly StartType Pit = new StartType("PIT", "Pit Stop");
            public static readonly StartType RegularStart = new StartType("START", "Race Start");
            public static readonly StartType HotlapStart = new StartType("HOTLAP_START", "Hotlap Start");

            public static readonly BindingList<StartType> Values = new BindingList<StartType>(new[] {
                Pit, RegularStart, HotlapStart
            });

            private StartType(string value, string name) {
                Value = value;
                Name = name;
            }

            public override string ToString() {
                return Name;
            }
        }

        public class BasicProperties : RaceIniProperties {
            public string DriverName, DriverNationality;
            public string CarId, CarSkinId, CarSetupId;
            public string TrackId, TrackConfigurationId;

            public override void Set(IniFile file) {
                var raceSection = file["RACE"];
                raceSection.SetId("MODEL", CarId);
                raceSection.SetId("SKIN", CarSkinId);
                raceSection.SetId("TRACK", TrackId);
                raceSection.Set("CONFIG_TRACK", TrackConfigurationId ?? "");

                var playerCarSection = file["CAR_0"];
                playerCarSection.SetId("SETUP", CarSetupId ?? "");
                playerCarSection.SetId("SKIN", CarSkinId);
                playerCarSection.Set("MODEL", "-");
                playerCarSection.Set("MODEL_CONFIG", "");
                playerCarSection.Set("DRIVER_NAME", DriverName);
                playerCarSection.Set("NATIONALITY", DriverNationality);
            }
        }

        public class AiCar {
            public string CarId, SkinId = "", Setup = "", DriverName = "", Nationality = "";
            public int AiLevel = 100;
        }

        public abstract class BaseModeProperties : RaceIniProperties {
            public bool? Penalties = true;
            public bool? FixedSetup = false;

            /// <summary>
            /// Session duration in minutes.
            /// </summary>
            public int Duration = 0;

            public override void Set(IniFile file) {
                var raceSection = file["RACE"];
                raceSection.Set("CARS", 1);
                raceSection.Set("DRIFT_MODE", false);
                raceSection.Set("FIXED_SETUP", FixedSetup);
                raceSection.Set("PENALTIES", Penalties);
            }

            protected void SetGhostCar(IniFile file, bool? ghostCarEnabled, double? ghostCarAdvantage = null) {
                var ghostSection = file["GHOST_CAR"];
                ghostSection.Set("RECORDING", ghostCarEnabled);
                ghostSection.Set("PLAYING", ghostCarEnabled);
                ghostSection.Set("SECONDS_ADVANTAGE", ghostCarAdvantage);
                ghostSection.Set("LOAD", ghostCarEnabled);
                ghostSection.Set("FILE", "");
                ghostSection.Set("ENABLED", false);
            }
        }

        public class OnlineProperties : BaseModeProperties {
            public string ServerIp, Guid, Password;
            public int ServerPort;

            public override void Set(IniFile file) {
                SetGhostCar(file, false);

                var raceSection = file["REMOTE"];
                raceSection.Set("SERVER_IP", ServerIp);
                raceSection.Set("SERVER_PORT", ServerPort);
                raceSection.Set("REQUESTED_CAR", file["RACE"].Get("MODEL"));
                raceSection.Set("GUID", Guid);
                raceSection.Set("PASSWORD", Password);
                raceSection.Set("ACTIVE", true);
            }
        }

        public class PracticeProperties : BaseModeProperties {
            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file, false);
                base.Set(file);

                var grooveSection = file["GROOVE"];
                grooveSection.Set("VIRTUAL_LAPS", 10);
                grooveSection.Set("MAX_LAPS", 30);
                grooveSection.Set("STARTING_LAPS", 0);

                var sessionSection = file["SESSION_0"];
                sessionSection.Set("NAME", "Practice");
                sessionSection.Set("TYPE", SessionType.Practice);
                sessionSection.Set("DURATION_MINUTES", Duration);
                sessionSection.Set("SPAWN_SET", StartType.Value);
            }
        }

        public class HotlapProperties : BaseModeProperties {
            public bool? GhostCar = true;

            /// <summary>
            /// Ghost car advantage in seconds.
            /// </summary>
            public double? GhostCarAdvantage = 0.0;

            public override void Set(IniFile file) {
                SetGhostCar(file, GhostCar, GhostCarAdvantage);
                base.Set(file);

                var grooveSection = file["GROOVE"];
                grooveSection.Set("VIRTUAL_LAPS", 10);
                grooveSection.Set("MAX_LAPS", 1);
                grooveSection.Set("STARTING_LAPS", 1);

                var sessionSection = file["SESSION_0"];
                sessionSection.Set("NAME", "Hotlap");
                sessionSection.Set("TYPE", SessionType.Hotlap);
                sessionSection.Set("DURATION_MINUTES", Duration);
                sessionSection.Set("SPAWN_SET", "HOTLAP_START");
            }
        }

        public class TimeAttackProperties : BaseModeProperties {
            public override void Set(IniFile file) {
                SetGhostCar(file, false);
                base.Set(file);

                var grooveSection = file["GROOVE"];
                grooveSection.Set("VIRTUAL_LAPS", 10);
                grooveSection.Set("MAX_LAPS", 1);
                grooveSection.Set("STARTING_LAPS", 1);

                var sessionSection = file["SESSION_0"];
                sessionSection.Set("NAME", "Time Attack");
                sessionSection.Set("TYPE", SessionType.TimeAttack);
                sessionSection.Set("DURATION_MINUTES", Duration);
                sessionSection.Set("SPAWN_SET", "START");
            }
        }

        public class DriftProperties : BaseModeProperties {
            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file, false);
                base.Set(file);

                var grooveSection = file["GROOVE"];
                grooveSection.Set("VIRTUAL_LAPS", 10);
                grooveSection.Set("MAX_LAPS", 1);
                grooveSection.Set("STARTING_LAPS", 1);

                var sessionSection = file["SESSION_0"];
                sessionSection.Set("NAME", "Drift Session");
                sessionSection.Set("TYPE", SessionType.Drift);
                sessionSection.Set("DURATION_MINUTES", Duration);
                sessionSection.Set("SPAWN_SET", StartType.Value);
            }
        }

        public class RaceProperties : BaseModeProperties {
            public IEnumerable<AiCar> BotCars;
            public int AiLevel = 90, RaceLaps = 5, StartingPosition;

            public override void Set(IniFile file) {
                SetGhostCar(file, false);

                var grooveSection = file["GROOVE"];
                grooveSection.Set("VIRTUAL_LAPS", 10);
                grooveSection.Set("MAX_LAPS", 30);
                grooveSection.Set("STARTING_LAPS", 0);

                var raceSection = file["RACE"];
                raceSection.Set("CARS", BotCars.Count() + 1);
                raceSection.Set("AI_LEVEL", AiLevel);
                raceSection.Set("DRIFT_MODE", false);
                raceSection.Set("RACE_LAPS", RaceLaps);
                raceSection.Set("FIXED_SETUP", FixedSetup);
                raceSection.Set("PENALTIES", Penalties);

                var sessionSection = file["SESSION_0"];
                sessionSection.Set("NAME", "Quick Race");
                sessionSection.Set("TYPE", SessionType.Race);
                sessionSection.Set("LAPS", RaceLaps);
                sessionSection.Set("STARTING_POSITION", StartingPosition);
                sessionSection.Set("DURATION_MINUTES", Duration);
                sessionSection.Set("SPAWN_SET", "START");

                var j = 0;
                foreach (var botCar in BotCars) {
                    var botSection = file["CAR_" + ++j];
                    botSection.SetId("MODEL", botCar.CarId);
                    botSection.SetId("SKIN", botCar.SkinId);
                    botSection.SetId("SETUP", botCar.Setup);
                    botSection.Set("MODEL_CONFIG", "");
                    botSection.Set("AI_LEVEL", botCar.AiLevel);
                    botSection.Set("DRIVER_NAME", botCar.DriverName);
                    botSection.Set("NATIONALITY", botCar.Nationality);
                }
            }
        }

        public class ConditionProperties : RaceIniProperties {
            public double? SunAngle, TimeMultipler, CloudSpeed;
            public double? RoadTemperature, AmbientTemperature;
            public string WeatherName;

            public override void Set(IniFile file) {
                var temperatureSection = file["TEMPERATURE"];
                temperatureSection.Set("ROAD", RoadTemperature, "F2");
                temperatureSection.Set("AMBIENT", AmbientTemperature, "F2");

                var lightingSection = file["LIGHTING"];
                lightingSection.Set("SUN_ANGLE", SunAngle, "F2");
                lightingSection.Set("TIME_MULT", TimeMultipler, "F1");
                lightingSection.Set("CLOUD_SPEED", CloudSpeed, "F3");

                var weatherSection = file["WEATHER"];
                weatherSection.SetId("NAME", WeatherName);
            }

            public static double GetSunAngle(double seconds) {
                // 08:00 → -80
                // 13:00 → 0 (46800)
                // 13:30 → 8
                // 14:00 → 16 (50400)
                // 14:30 → 24
                // 15:00 → 32
                // 18:00 → 80
                // so, linear
                return 16.0 * (seconds - 46800.0) / (50400.0 - 46800.0);
            }

            public static double GetSeconds(double sunAngle) {
                return sunAngle * (50400.0 - 46800.0) / 16.0 + 46800.0;
            }

            public static double GetRoadTemperature(double seconds, double ambientTemperature, double weatherCoefficient = 1.0) {
                var wc = Equals(weatherCoefficient, 0.0) ? 1.0 : weatherCoefficient;
                var tc = (seconds / 60.0 / 60.0 - 7.0) * 0.04167;
                return ambientTemperature + 20.0 * wc * (1.0 - tc) * (
                        (0.4 * Math.Exp(-6.0 * tc) * Math.Sin(6.0 * tc) + 0.1) * Math.Sin(0.9 * tc) * ambientTemperature / 1.5);
            }
        }

        public class TrackProperties : RaceIniProperties {
            public int? Preset;
            public double? SessionStart, Randomness, LapGain, SessionTransfer;

            public override void Set(IniFile file) {
                var section = file["DYNAMIC_TRACK"];
                section.Set("PRESET", Preset);
                section.Set("SESSION_START", SessionStart);
                section.Set("RANDOMNESS", Randomness);
                section.Set("LAP_GAIN", LapGain);
                section.Set("SESSION_TRANSFER", SessionTransfer);
            }
        }

        public class TrackPropertiesPreset {
            public readonly TrackProperties Properties;

            public string Name { get; }

            public TrackPropertiesPreset(string name, TrackProperties properties) {
                Name = name;
                Properties = properties;
            }

            public override string ToString() {
                return Name;
            }
        }

        private static BindingList<TrackPropertiesPreset> _defaultTrackPropertiesPresets;

        public static TrackPropertiesPreset GetDefaultTrackPropertiesPreset() {
            return DefaultTrackPropertiesPresets.FirstOrDefault(x => x.Name == "Optimum") ??
                   DefaultTrackPropertiesPresets.FirstOrDefault();
        }

        public static BindingList<TrackPropertiesPreset> DefaultTrackPropertiesPresets {
            get {
                if (_defaultTrackPropertiesPresets != null) return _defaultTrackPropertiesPresets;

                return _defaultTrackPropertiesPresets = new BindingList<TrackPropertiesPreset>(new[] {
                    new TrackPropertiesPreset("Dusty", new TrackProperties {
                        Preset = 0,
                        SessionStart = 86.0,
                        Randomness = 1.0,
                        LapGain = 18.0,
                        SessionTransfer = 50.0
                    }),
                    new TrackPropertiesPreset("Old", new TrackProperties {
                        Preset = 1,
                        SessionStart = 89.0,
                        Randomness = 3.0,
                        LapGain = 50.0,
                        SessionTransfer = 80.0
                    }),
                    new TrackPropertiesPreset("Slow", new TrackProperties {
                        Preset = 2,
                        SessionStart = 96.0,
                        Randomness = 1.0,
                        LapGain = 60.0,
                        SessionTransfer = 80.0
                    }),
                    new TrackPropertiesPreset("Green", new TrackProperties {
                        Preset = 3,
                        SessionStart = 95.0,
                        Randomness = 2.0,
                        LapGain = 10.0,
                        SessionTransfer = 90.0
                    }),
                    new TrackPropertiesPreset("Fast", new TrackProperties {
                        Preset = 4,
                        SessionStart = 98.0,
                        Randomness = 2.0,
                        LapGain = 20.0,
                        SessionTransfer = 80.0
                    }),
                    new TrackPropertiesPreset("Optimum", new TrackProperties {
                        Preset = 5,
                        SessionStart = 100.0,
                        Randomness = 0.0,
                        LapGain = 1.0,
                        SessionTransfer = 100.0
                    })
                });
            }
        }

        public class AssistsProperties : AdditionalProperties {
            public bool IdealLine;
            public bool AutoBlip;
            public double StabilityControl;
            public bool AutoBrake;
            public bool AutoShifter;
            public AssistState Abs;
            public AssistState TractionControl;
            public bool AutoClutch;
            public bool VisualDamage;
            public double Damage;
            public bool FuelConsumption;
            public double TyreWearMultipler;
            public bool TyreBlankets;
            public double SlipSteamMultipler;

            private IniFile ToIniFile() {
                return new IniFile {
                    ["ASSISTS"] = {
                        ["IDEAL_LINE"] = IdealLine,
                        ["AUTO_BLIP"] = AutoBlip,
                        ["STABILITY_CONTROL"] = StabilityControl,
                        ["AUTO_BRAKE"] = AutoBrake,
                        ["AUTO_SHIFTER"] = AutoShifter,
                        ["ABS"] = Abs.GetDescription(),
                        ["TRACTION_CONTROL"] = TractionControl.GetDescription(),
                        ["AUTO_CLUTCH"] = AutoClutch,
                        ["VISUALDAMAGE"] = VisualDamage,
                        ["DAMAGE"] = Damage,
                        ["FUEL_RATE"] = FuelConsumption,
                        ["TYRE_WEAR"] = TyreWearMultipler,
                        ["TYRE_BLANKETS"] = TyreBlankets,
                        ["SLIPSTREAM"] = SlipSteamMultipler,
                    }
                };
            }

            public override IDisposable Set() {
                var assistsIniFilename = FileUtils.GetAssistsIniFilename();
                var result = FileUtils.RestoreLater(assistsIniFilename);
                ToIniFile().Save(assistsIniFilename);
                return result;
            }
        }

        public class ReplayProperties {
            public string Filename, Name, TrackId, TrackConfiguration;

            internal void Set(IniFile file) {
                file["REPLAY"].Set("ACTIVE", true);
                file["REPLAY"].Set("FILENAME", Name);

                // another weirdness of Assetto Corsa
                file["RACE"].Set("TRACK", TrackId);
                file["RACE"].Set("CONFIG_TRACK", TrackConfiguration);
            }
        }
    }
}
