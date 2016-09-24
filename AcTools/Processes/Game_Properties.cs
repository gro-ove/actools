using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

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

        public enum JumpStartPenaltyType {
            None = 0,
            Pits = 1,
            DriveThrough = 2
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
                var section = file["RACE"];
                section.SetId("MODEL", CarId ?? "");
                section.SetId("MODEL_CONFIG", "");
                section.SetId("SKIN", CarSkinId ?? "");
                section.SetId("TRACK", TrackId ?? "");
                section.SetId("CONFIG_TRACK", TrackConfigurationId ?? "");

                if (!section.ContainsKey("AI_LEVEL")) {
                    section.Set("AI_LEVEL", 100);
                }

                file["CAR_0"] = new IniFileSection {
                    ["SETUP"] = CarSetupId?.ToLowerInvariant() ?? "",
                    ["SKIN"] = CarSkinId?.ToLowerInvariant(),
                    ["MODEL"] = "-",
                    ["MODEL_CONFIG"] = "",
                    ["DRIVER_NAME"] = DriverName,
                    ["NATIONALITY"] = DriverNationality
                };
            }
        }

        public class AiCar {
            public string CarId, SkinId = "", Setup = "", DriverName = "", Nationality = "";
            public int AiLevel = 100;
        }

        public abstract class BaseModeProperties : RaceIniProperties {
            public bool? Penalties = true;
            public bool? FixedSetup = false;
            public JumpStartPenaltyType? JumpStartPenalty = JumpStartPenaltyType.None;

            /// <summary>
            /// Session duration in minutes.
            /// </summary>
            public int Duration = 0;

            public override void Set(IniFile file) {
                var section = file["RACE"];
                section.Set("CARS", 1);
                section.Set("DRIFT_MODE", false);
                section.Set("FIXED_SETUP", FixedSetup);
                section.Set("PENALTIES", Penalties);
                section.Set("JUMP_START_PENALTY", JumpStartPenalty);
            }

            protected void SetGhostCar(IniFile file, bool playing = false, bool recording = false, double? advantage = null) {
                var section = file["GHOST_CAR"];
                section.Set("RECORDING", recording);
                section.Set("PLAYING", playing);
                section.Set("SECONDS_ADVANTAGE", advantage, "0.###");
                section.Set("LOAD", playing || recording);
                section.Set("FILE", "");
                section.Set("ENABLED", false);
            }

            protected virtual void SetGroove(IniFile file, int virtualLaps = 10, int maxLaps = 1, int startingLaps = 1) {
                file["GROOVE"] = new IniFileSection {
                    ["VIRTUAL_LAPS"] = virtualLaps,
                    ["MAX_LAPS"] = maxLaps,
                    ["STARTING_LAPS"] = startingLaps
                };
            }
        }

        public class OnlineProperties : BaseModeProperties {
            public string ServerName, ServerIp, Guid, Password, RequestedCar;
            public int ServerPort;
            public int? ServerHttpPort;

            public override void Set(IniFile file) {
                SetGhostCar(file);

                var section = file["REMOTE"];
                section.Set("SERVER_IP", ServerIp);
                section.Set("SERVER_PORT", ServerPort);

                /*if (ServerName != null) {
                    raceSection.Set("SERVER_NAME", ServerName);
                } else {
                    raceSection.Remove("SERVER_NAME");
                }*/

                section.Remove("SERVER_NAME");

                if (ServerHttpPort.HasValue) {
                    section.Set("SERVER_HTTP_PORT", ServerHttpPort);
                } else {
                    section.Remove("SERVER_HTTP_PORT");
                }

                section.Set("REQUESTED_CAR", RequestedCar ?? file["RACE"].GetPossiblyEmpty("MODEL"));
                section.Set("GUID", Guid);
                section.Set("PASSWORD", Password);
                section.Set("ACTIVE", true);
            }
        }

        public class PracticeProperties : BaseModeProperties {
            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file, 10, 30, 0);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", "Practice");
                section.Set("TYPE", SessionType.Practice);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", StartType.Value);
            }
        }

        public class HotlapProperties : BaseModeProperties {
            public string SessionName = "Hotlap";

            public bool GhostCar = true;

            public bool? RecordGhostCar = null;

            /// <summary>
            /// Ghost car advantage in seconds.
            /// </summary>
            public double? GhostCarAdvantage = 0.0;

            public override void Set(IniFile file) {
                SetGhostCar(file, GhostCar, RecordGhostCar ?? GhostCar, GhostCarAdvantage);
                SetGroove(file);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", SessionName);
                section.Set("TYPE", SessionType.Hotlap);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", "HOTLAP_START");
            }
        }

        public class TimeAttackProperties : BaseModeProperties {
            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", "Time Attack");
                section.Set("TYPE", SessionType.TimeAttack);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", "START");
            }
        }

        public class DriftProperties : BaseModeProperties {
            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", "Drift Session");
                section.Set("TYPE", SessionType.Drift);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", StartType.Value);
            }
        }

        public class RaceProperties : BaseModeProperties {
            public IEnumerable<AiCar> BotCars;
            public int AiLevel = 90, RaceLaps = 5, StartingPosition;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file, 10, 30, 0);
                SetRace(file);
                SetSessions(file);
                SetBots(file);
            }

            protected virtual void SetRace(IniFile file) {
                var section = file["RACE"];
                section.Set("CARS", BotCars.Count() + 1);
                section.Set("AI_LEVEL", AiLevel);
                section.Set("DRIFT_MODE", false);
                section.Set("RACE_LAPS", RaceLaps);
                section.Set("FIXED_SETUP", FixedSetup);
                section.Set("PENALTIES", Penalties);
                section.Set("JUMP_START_PENALTY", JumpStartPenalty);
            }

            protected virtual void SetSessions(IniFile file) {
                file["SESSION_0"] = new IniFileSection {
                    ["NAME"] = "Quick Race",
                    ["DURATION_MINUTES"] = Duration,
                    ["SPAWN_SET"] = StartType.RegularStart.Value,
                    ["TYPE"] = SessionType.Race,
                    ["LAPS"] = RaceLaps,
                    ["STARTING_POSITION"] = StartingPosition
                };
            }

            protected virtual void SetBots(IniFile file) {
                var j = 0;
                foreach (var botCar in BotCars) {
                    file["CAR_" + ++j] = new IniFileSection {
                        ["MODEL"] = botCar.CarId?.ToLowerInvariant(),
                        ["SKIN"] = botCar.SkinId?.ToLowerInvariant(),
                        ["SETUP"] = botCar.Setup?.ToLowerInvariant(),
                        ["MODEL_CONFIG"] = "",
                        ["AI_LEVEL"] = botCar.AiLevel,
                        ["DRIVER_NAME"] = botCar.DriverName,
                        ["NATIONALITY"] = botCar.Nationality
                    };
                }
            }
        }

        public class TrackdayProperties : RaceProperties {
            protected override void SetSessions(IniFile file) {
                file["SESSION_0"] = new IniFileSection {
                    ["NAME"] = "Track Day",
                    ["DURATION_MINUTES"] = 720,
                    ["SPAWN_SET"] = StartType.Pit.Value,
                    ["TYPE"] = SessionType.Qualification
                };
            }
        }

        public class WeekendProperties : RaceProperties {
            public int PracticeDuration = 10,
                    QualificationDuration = 15;

            public StartType PracticeStartType = StartType.Pit,
                    QualificationStartType = StartType.Pit;

            protected override void SetSessions(IniFile file) {
                var i = 0;

                if (PracticeDuration > 0) {
                    file["SESSION_" + i++] = new IniFileSection {
                        ["NAME"] = "Practice",
                        ["DURATION_MINUTES"] = PracticeDuration,
                        ["SPAWN_SET"] = PracticeStartType.Value,
                        ["TYPE"] = SessionType.Practice
                    };
                }

                if (QualificationDuration > 0) {
                    file["SESSION_" + i++] = new IniFileSection {
                        ["NAME"] = "Qualifying",
                        ["DURATION_MINUTES"] = QualificationDuration,
                        ["SPAWN_SET"] = QualificationStartType.Value,
                        ["TYPE"] = SessionType.Qualification
                    };
                }

                file["SESSION_" + i] = new IniFileSection {
                    ["NAME"] = "Race",
                    ["DURATION_MINUTES"] = Duration,
                    ["SPAWN_SET"] = StartType.RegularStart.Value,
                    ["TYPE"] = SessionType.Race,
                    ["LAPS"] = RaceLaps,
                };
            }
        }

        public class ConditionProperties : RaceIniProperties {
            public double? SunAngle, TimeMultipler, CloudSpeed;
            public double? RoadTemperature, AmbientTemperature;
            public string WeatherName;

            public override void Set(IniFile file) {
                var temperatureSection = file["TEMPERATURE"];
                temperatureSection.Set("ROAD", RoadTemperature, "F0");
                temperatureSection.Set("AMBIENT", AmbientTemperature, "F0");

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

        public class TrackPropertiesPreset : IWithId<int?> {
            [NotNull]
            public readonly TrackProperties Properties;

            [NotNull]
            public string Name { get; }

            public TrackPropertiesPreset([NotNull] string name, [NotNull] TrackProperties properties) {
                Name = name;
                Properties = properties;
            }

            public override string ToString() {
                return Name;
            }

            [CanBeNull]
            public int? Id => Properties.Preset;
        }

        private static BindingList<TrackPropertiesPreset> _defaultTrackPropertiesPresets;

        public static TrackPropertiesPreset GetDefaultTrackPropertiesPreset() {
            return DefaultTrackPropertiesPresets.FirstOrDefault(x => x.Name == "Optimum") ??
                   DefaultTrackPropertiesPresets.FirstOrDefault();
        }

        // TODO: rework; move everything related to actools?
        public static BindingList<TrackPropertiesPreset> DefaultTrackPropertiesPresets
            => _defaultTrackPropertiesPresets ?? (_defaultTrackPropertiesPresets = new BindingList<TrackPropertiesPreset>(new[] {
                new TrackPropertiesPreset("Dusty", new TrackProperties {
                    Preset = 0,
                    SessionStart = 86.0,
                    Randomness = 1.0,
                    LapGain = 30.0,
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
                    LapGain = 300.0,
                    SessionTransfer = 80.0
                }),
                new TrackPropertiesPreset("Green", new TrackProperties {
                    Preset = 3,
                    SessionStart = 95.0,
                    Randomness = 2.0,
                    LapGain = 132.0,
                    SessionTransfer = 90.0
                }),
                new TrackPropertiesPreset("Fast", new TrackProperties {
                    Preset = 4,
                    SessionStart = 98.0,
                    Randomness = 2.0,
                    LapGain = 700.0,
                    SessionTransfer = 80.0
                }),
                new TrackPropertiesPreset("Optimum", new TrackProperties {
                    Preset = 5,
                    SessionStart = 100.0,
                    Randomness = 0.0,
                    LapGain = 1.0,
                    SessionTransfer = 100.0
                })
            }));

        public class AssistsProperties : AdditionalProperties {
            public bool IdealLine;
            public bool AutoBlip;
            public int StabilityControl;
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
                        ["ABS"] = (int)Abs,
                        ["TRACTION_CONTROL"] = (int)TractionControl,
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
                ToIniFile().Save(FileUtils.GetAssistsIniFilename());
                return null;
            }

            public string GetDescription() {
                return $"(TyreBlankets={TyreBlankets}, ABS={(int)Abs}, TC={(int)TractionControl})";
            }
        }

        public class BenchmarkProperties {
            internal void Set(IniFile file) {
                file["BENCHMARK"].Set("ACTIVE", true);
            }
        }

        public class ReplayProperties {
            public string Filename, Name, TrackId, TrackConfiguration, WeatherId;

            internal void Set(IniFile file) {
                file["REPLAY"].Set("ACTIVE", true);
                file["REPLAY"].Set("FILENAME", Name);

                // for custom clouds
                if (WeatherId != null) {
                    file["WEATHER"].Set("NAME", WeatherId);
                }

                // another weirdness of Assetto Corsa
                file["RACE"].SetId("TRACK", TrackId);
                file["RACE"].SetId("CONFIG_TRACK", TrackConfiguration);
            }
        }
    }
}
