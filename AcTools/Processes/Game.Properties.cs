using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcTools.Processes {
    public interface INationCodeProvider {
        [CanBeNull]
        string GetNationCode([CanBeNull] string country);
    }

    public partial class Game {
        public static INationCodeProvider NationCodeProvider { get; set; }

        public enum SessionType : byte {
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
            Drift = 6,

            [Description("Drag Race")]
            Drag = 7
        }

        public enum JumpStartPenaltyType {
            None = 0,
            Pits = 1,
            DriveThrough = 2
        }

        public class StartType : IWithId {
            public string Name { get; }

            public static readonly StartType Pit = new StartType("PIT", "Pits");
            public static readonly StartType RegularStart = new StartType("START", "Starting Line");
            public static readonly StartType HotlapStart = new StartType("HOTLAP_START", "Hotlap Start");

            public static readonly StartType[] Values = {
                Pit, RegularStart, HotlapStart
            };

            private StartType(string value, string name) {
                Id = value;
                Name = name;
            }

            public override string ToString() {
                return Name;
            }

            public string Id { get; }
        }

        [CanBeNull]
        private static string GetNationCode([CanBeNull] string country) {
            return NationCodeProvider?.GetNationCode(country) ?? country?.Substring(0, Math.Min(3, country.Length)).ToUpper();
        }

        public class BasicProperties : RaceIniProperties {
            [CanBeNull]
            public string DriverName, DriverNationality, DriverNationCode,
                    CarId, CarSkinId, CarSetupId,
                    TrackId, TrackConfigurationId;

            [CanBeNull]
            public string CarSetupFilename;

            public double Ballast, Restrictor;
            public bool UseMph;

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

                file["CAR_0"] = new IniFileSection(null) {
                    ["SETUP"] = CarSetupId?.ToLowerInvariant() ?? "",
                    ["SKIN"] = CarSkinId?.ToLowerInvariant(),
                    ["MODEL"] = "-",
                    ["MODEL_CONFIG"] = "",
                    ["BALLAST"] = Ballast,
                    ["RESTRICTOR"] = Restrictor,
                    ["DRIVER_NAME"] = DriverName,
                    ["NATION_CODE"] = DriverNationCode ?? GetNationCode(DriverNationality),
                    ["NATIONALITY"] = DriverNationality
                };

                if (!string.IsNullOrWhiteSpace(CarSetupFilename)) {
                    file["CAR_0"].Set("_EXT_SETUP_FILENAME", CarSetupFilename);
                }

                file["OPTIONS"].Set("USE_MPH", UseMph);
            }
        }

        public class AiCar {
            [CanBeNull]
            public string CarId, SkinId = "", Setup = "", DriverName = "", Nationality = "", NationCode;

            public double AiLevel = 100, AiAggression = 0;
            public double Ballast, Restrictor;
        }

        public abstract class BaseModeProperties : RaceIniProperties {
            public bool? Penalties = true;
            public bool? FixedSetup = false;
            public JumpStartPenaltyType? JumpStartPenalty = JumpStartPenaltyType.None;

            /// <summary>
            /// Session duration in minutes.
            /// </summary>
            public double Duration = 0;

            public override void Set(IniFile file) {
                var section = file["RACE"];
                section.Set("CARS", 1);
                section.Set("DRIFT_MODE", false);
                section.Set("FIXED_SETUP", FixedSetup);
                section.Set("PENALTIES", Penalties);
                section.Set("JUMP_START_PENALTY", JumpStartPenalty);
                
                file["HEADER"].Set("__CM_FEATURE_SET", 1);
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
                file["GROOVE"] = new IniFileSection(null) {
                    ["VIRTUAL_LAPS"] = virtualLaps,
                    ["MAX_LAPS"] = maxLaps,
                    ["STARTING_LAPS"] = startingLaps
                };
            }

            protected virtual void SetBots(IniFile file, IEnumerable<AiCar> bots) {
                file.SetSections("CAR", 1, from car in bots
                    select new IniFileSection(null) {
                        ["MODEL"] = car.CarId?.ToLowerInvariant(),
                        ["SKIN"] = car.SkinId?.ToLowerInvariant(),
                        ["SETUP"] = car.Setup?.ToLowerInvariant(),
                        ["MODEL_CONFIG"] = "",
                        ["AI_LEVEL"] = car.AiLevel,
                        ["AI_AGGRESSION"] = car.AiAggression,
                        ["DRIVER_NAME"] = car.DriverName,
                        ["BALLAST"] = car.Ballast,
                        ["RESTRICTOR"] = car.Restrictor,
                        ["NATION_CODE"] = car.NationCode ?? GetNationCode(car.Nationality),
                        ["NATIONALITY"] = car.Nationality
                    });
            }
        }

        public class OnlineProperties : BaseModeProperties {
            [CanBeNull]
            public string ServerName, ServerIp, Guid, Password, RequestedCar, SessionName;

            public int ServerPort;
            public int? ServerHttpPort;
            public bool ExtendedMode;
            public string CspFeaturesList;
            public string CspReplayClipUploadUrl;
            public string BackgroundImage;

            public override void Set(IniFile file) {
                SetGhostCar(file);

                {
                    var section = file["REMOTE"];
                    section.Set("SERVER_IP", ServerIp);
                    section.Set("SERVER_PORT", ServerPort);

                    if (ServerName != null) {
                        section.Set("SERVER_NAME", ServerName);
                    } else {
                        section.Remove("SERVER_NAME");
                    }

                    if (ServerHttpPort.HasValue) {
                        section.Set("SERVER_HTTP_PORT", ServerHttpPort);
                    } else {
                        section.Remove("SERVER_HTTP_PORT");
                    }

                    section.Set("REQUESTED_CAR", RequestedCar ?? file["RACE"].GetPossiblyEmpty("MODEL"));
                    section.Set("GUID", Guid);
                    section.Set("PASSWORD", Password);
                    section.Set("ACTIVE", true);
                    section.Set("__CM_EXTENDED", ExtendedMode);
                    section.SetOrRemove("__FEATURES", CspFeaturesList.Or(null));
                    section.SetOrRemove("__CLIPS_UPLOAD_URL", string.IsNullOrWhiteSpace(CspReplayClipUploadUrl) ? null : JsonConvert.SerializeObject(CspReplayClipUploadUrl));
                }

                {
                    // Specially for SimRacers.es app
                    var section = file["SESSION_0"];
                    section.Set("NAME", "Nothing");
                    section.Set("TYPE", SessionType.Practice);
                    section.Set("DURATION_MINUTES", Duration);
                }

                if (!string.IsNullOrWhiteSpace(BackgroundImage)) {
                    file["OPTIONS"].Set("__BACKGROUND_IMAGE", $"'{BackgroundImage.Replace("\'", "\\'")}'");
                }
            }
        }

        public class PracticeProperties : BaseModeProperties {
            [CanBeNull]
            public string SessionName = "Practice";

            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file, 10, 30, 0);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", SessionName);
                section.Set("TYPE", SessionType.Practice);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", StartType.Id);
            }
        }

        public class HotlapProperties : BaseModeProperties {
            [CanBeNull]
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
            [CanBeNull]
            public string SessionName = "Time Attack";

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", SessionName);
                section.Set("TYPE", SessionType.TimeAttack);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", "START");
            }
        }

        public class DragProperties : BaseModeProperties {
            [CanBeNull]
            public string SessionName = "Drag Race";

            public StartType StartType = StartType.RegularStart;
            public double AiLevel = 100;
            public int MatchesCount = 10;

            [CanBeNull]
            public AiCar BotCar;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file);
                SetRace(file);
                SetSessions(file);
                SetBots(file, new[] {
                    BotCar ?? new AiCar {
                        AiLevel = 100,
                        DriverName = "Bot",
                        CarId = file["RACE"].GetNonEmpty("MODEL"),
                        SkinId = file["RACE"].GetNonEmpty("SKIN")
                    }
                });
            }

            protected void SetRace(IniFile file) {
                var section = file["RACE"];
                section.Set("CARS", 2);
                section.Set("AI_LEVEL", AiLevel);
                section.Set("DRIFT_MODE", false);
                section.Set("FIXED_SETUP", FixedSetup);
                section.Set("PENALTIES", true);
                section.Set("JUMP_START_PENALTY", 0);
            }

            protected void SetSessions(IniFile file) {
                file["SESSION_0"] = new IniFileSection(null) {
                    ["NAME"] = SessionName,
                    ["TYPE"] = SessionType.Drag,
                    ["SPAWN_SET"] = StartType.Id,
                    ["MATCHES"] = MatchesCount
                };
            }
        }

        public class DriftProperties : BaseModeProperties {
            [CanBeNull]
            public string SessionName = "Drift Session";

            public StartType StartType = StartType.Pit;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file);

                base.Set(file);

                var section = file["SESSION_0"];
                section.Set("NAME", SessionName);
                section.Set("TYPE", SessionType.Drift);
                section.Set("DURATION_MINUTES", Duration);
                section.Set("SPAWN_SET", StartType.Id);
            }
        }

        public class RaceProperties : BaseModeProperties {
            public string SessionName = "Quick Race";
            public IEnumerable<AiCar> BotCars;
            public double AiLevel = 90;
            public int RaceLaps = 5, StartingPosition;

            public override void Set(IniFile file) {
                SetGhostCar(file);
                SetGroove(file, 10, 30, 0);
                SetRace(file);
                SetSessions(file);
                SetBots(file, BotCars);
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
                file["SESSION_0"] = new IniFileSection(null) {
                    ["NAME"] = SessionName,
                    ["DURATION_MINUTES"] = Duration,
                    ["SPAWN_SET"] = StartType.RegularStart.Id,
                    ["TYPE"] = SessionType.Race,
                    ["LAPS"] = RaceLaps,
                    ["STARTING_POSITION"] = StartingPosition
                };
            }
        }

        public static readonly string TrackDaySessionName = "car";

        public class TrackdayProperties : RaceProperties {
            public bool UsePracticeSessionType = false;
            public double SpeedLimit = 0.0;

            protected override void SetSessions(IniFile file) {
                file["SESSION_0"] = new IniFileSection(null) {
                    ["NAME"] = TrackDaySessionName,
                    ["DURATION_MINUTES"] = 720,
                    ["SPAWN_SET"] = StartType.Pit.Id,
                    ["TYPE"] = UsePracticeSessionType ? SessionType.Practice : SessionType.Qualification,
                    ["__SPEED_LIMIT"] = SpeedLimit
                };
            }
        }

        public class WeekendProperties : RaceProperties {
            public int PracticeDuration = 10;
            public int QualificationDuration = 15;
            public StartType PracticeStartType = StartType.Pit;
            public StartType QualificationStartType = StartType.Pit;

            private IEnumerable<IniFileSection> GetSessions() {
                if (PracticeDuration > 0) {
                    yield return new IniFileSection(null) {
                        ["NAME"] = "Practice",
                        ["DURATION_MINUTES"] = PracticeDuration,
                        ["SPAWN_SET"] = PracticeStartType.Id,
                        ["TYPE"] = SessionType.Practice
                    };
                }

                if (QualificationDuration > 0) {
                    yield return new IniFileSection(null) {
                        ["NAME"] = "Qualifying",
                        ["DURATION_MINUTES"] = QualificationDuration,
                        ["SPAWN_SET"] = QualificationStartType.Id,
                        ["TYPE"] = SessionType.Qualification
                    };
                }

                yield return new IniFileSection(null) {
                    ["NAME"] = "Race",
                    ["DURATION_MINUTES"] = Duration,
                    ["SPAWN_SET"] = StartType.RegularStart.Id,
                    ["TYPE"] = SessionType.Race,
                    ["LAPS"] = RaceLaps,
                };
            }

            protected override void SetSessions(IniFile file) {
                file.SetSections("SESSION", GetSessions());
            }
        }

        public class ConditionProperties : RaceIniProperties {
            public double? SunAngle, TimeMultipler, CloudSpeed;
            public double? RoadTemperature, AmbientTemperature;
            public double? WindDirectionDeg, WindSpeedMin, WindSpeedMax;
            public string WeatherName;

            public override void Set(IniFile file) {
                var temperatureSection = file["TEMPERATURE"];
                temperatureSection.Set("ROAD", RoadTemperature, "F0");
                temperatureSection.Set("AMBIENT", AmbientTemperature, "F0");

                var lightingSection = file["LIGHTING"];
                lightingSection.Remove("__CM_UNCLAMPED_SUN_ANGLE");
                lightingSection.Set("SUN_ANGLE", SunAngle, "F2");
                lightingSection.Set("TIME_MULT", TimeMultipler, "F1");
                lightingSection.Set("CLOUD_SPEED", CloudSpeed, "F3");

                var weatherSection = file["WEATHER"];
                weatherSection.SetId("NAME", WeatherName);

                var windSection = file["WIND"];
                windSection.Set("SPEED_KMH_MIN", WindSpeedMin);
                windSection.Set("SPEED_KMH_MAX", WindSpeedMax);
                windSection.Set("DIRECTION_DEG", WindDirectionDeg);
            }

            public static double GetSunAngle(double seconds) {
                // 08:00 → -80
                // 13:00 → 0 (46800)
                // 13:30 → 8
                // 14:00 → 16 (50400)
                // 14:30 → 24
                // 15:00 → 32
                // 18:00 → 80
                // So, linear
                return 16.0 * (seconds - 46800.0) / (50400.0 - 46800.0);
            }

            public static double GetSeconds(double sunAngle) {
                return sunAngle * (50400.0 - 46800.0) / 16.0 + 46800.0;
            }

            public static double GetRoadTemperature(double seconds, double ambientTemperature, double weatherCoefficient = 1.0) {
                if (seconds < CommonAcConsts.TimeMinimum || seconds > CommonAcConsts.TimeMaximum) {
                    var minTemperature = GetRoadTemperature(CommonAcConsts.TimeMinimum, ambientTemperature, weatherCoefficient);
                    var maxTemperature = GetRoadTemperature(CommonAcConsts.TimeMaximum, ambientTemperature, weatherCoefficient);
                    var minValue = CommonAcConsts.TimeMinimum;
                    var maxValue = CommonAcConsts.TimeMaximum - 24 * 60 * 60;
                    if (seconds > CommonAcConsts.TimeMaximum) {
                        seconds -= 24 * 60 * 60;
                    }

                    return minTemperature + (maxTemperature - minTemperature) * (seconds - minValue) / (maxValue - minValue);
                }

                var time = (seconds / 60d / 60d - 7d) * 0.04167;
                return ambientTemperature * (1d + 5.33332 * (weatherCoefficient == 0d ? 1d : weatherCoefficient) * (1d - time) *
                        (Math.Exp(-6d * time) * Math.Sin(6d * time) + 0.25) * Math.Sin(0.9 * time));
            }
        }

        public class TrackProperties : RaceIniProperties, INotifyPropertyChanged {
            private int? _preset;

            public int? Preset {
                get => _preset;
                set {
                    if (value == _preset) return;
                    _preset = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrackPropertiesPreset.Id));
                }
            }

            private double? _sessionStart;

            public double? SessionStart {
                get => _sessionStart;
                set {
                    if (value.Equals(_sessionStart)) return;
                    _sessionStart = value;
                    OnPropertyChanged();
                }
            }

            private double? _randomness;

            public double? Randomness {
                get => _randomness;
                set {
                    if (value.Equals(_randomness)) return;
                    _randomness = value;
                    OnPropertyChanged();
                }
            }

            private double? _lapGain;

            public double? LapGain {
                get => _lapGain;
                set {
                    if (value.Equals(_lapGain)) return;
                    _lapGain = value;
                    OnPropertyChanged();
                }
            }

            private double? _sessionTransfer;

            public double? SessionTransfer {
                get => _sessionTransfer;
                set {
                    if (value.Equals(_sessionTransfer)) return;
                    _sessionTransfer = value;
                    OnPropertyChanged();
                }
            }

            public static TrackProperties Load(IniFile file) {
                return Load(file["DYNAMIC_TRACK"]);
            }

            public static TrackProperties Load(IniFileSection section) {
                return new TrackProperties {
                    Preset = section.GetIntNullable("Preset"),
                    SessionStart = section.GetDouble("SESSION_START", 95),
                    Randomness = section.GetDouble("RANDOMNESS", 2),
                    LapGain = section.GetDouble("LAP_GAIN", 10),
                    SessionTransfer = section.GetDouble("SESSION_TRANSFER", 90)
                };
            }

            public override void Set(IniFile file) {
                Set(file["DYNAMIC_TRACK"]);
            }

            public void Set(IniFileSection section) {
                if (Preset.HasValue) {
                    section.Set("PRESET", Preset);
                } else {
                    section.Remove("PRESET");
                }

                section.Set("SESSION_START", SessionStart);
                section.Set("RANDOMNESS", Randomness);
                section.Set("LAP_GAIN", LapGain);
                section.Set("SESSION_TRANSFER", SessionTransfer);
            }

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private static TrackPropertiesPreset[] _defaultTrackPropertiesPresets;

        public static TrackPropertiesPreset GetDefaultTrackPropertiesPreset() {
            return DefaultTrackPropertiesPresets.FirstOrDefault(x => x.Name == "Optimum") ??
                    DefaultTrackPropertiesPresets.FirstOrDefault();
        }

        // TODO: rework; move everything related to actools?
        public static IReadOnlyList<TrackPropertiesPreset> DefaultTrackPropertiesPresets
            => _defaultTrackPropertiesPresets ?? (_defaultTrackPropertiesPresets = new[] {
                new TrackPropertiesPreset("Dusty", new TrackProperties {
                    Preset = 0,
                    SessionStart = 86,
                    Randomness = 1,
                    LapGain = 30,
                    SessionTransfer = 50
                }),
                new TrackPropertiesPreset("Old", new TrackProperties {
                    Preset = 1,
                    SessionStart = 89,
                    Randomness = 3,
                    LapGain = 50,
                    SessionTransfer = 80
                }),
                new TrackPropertiesPreset("Slow", new TrackProperties {
                    Preset = 2,
                    SessionStart = 96,
                    Randomness = 1,
                    LapGain = 300,
                    SessionTransfer = 80
                }),
                new TrackPropertiesPreset("Green", new TrackProperties {
                    Preset = 3,
                    SessionStart = 95,
                    Randomness = 2,
                    LapGain = 132,
                    SessionTransfer = 90
                }),
                new TrackPropertiesPreset("Fast", new TrackProperties {
                    Preset = 4,
                    SessionStart = 98,
                    Randomness = 2,
                    LapGain = 700,
                    SessionTransfer = 80
                }),
                new TrackPropertiesPreset("Optimum", new TrackProperties {
                    Preset = 5,
                    SessionStart = 100,
                    Randomness = 0,
                    LapGain = 1,
                    SessionTransfer = 100
                })
            });

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
            public double FuelConsumption;
            public double TyreWearMultipler;
            public bool TyreBlankets;
            public double SlipSteamMultipler;

            public IniFile ToIniFile() {
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
                ToIniFile().Save(AcPaths.GetAssistsIniFilename());
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
            public double? SunAngle;

            // For internal references
            [CanBeNull]
            public string CarId;

            internal void Set(IniFile file) {
                file["REPLAY"].Set("ACTIVE", true);
                file["REPLAY"].Set("FILENAME", Name);

                // For custom clouds
                if (WeatherId != null) {
                    file["WEATHER"].Set("NAME", WeatherId);
                }

                // For custom clouds
                if (SunAngle.HasValue) {
                    var lightingSection = file["LIGHTING"];
                    lightingSection.Remove("__CM_UNCLAMPED_SUN_ANGLE");
                    lightingSection.Set("SUN_ANGLE", SunAngle, "F2");
                }

                // For car textures?
                var section = file["RACE"];
                section.SetId("MODEL", CarId);

                // Another weirdness of Assetto Corsa
                file["RACE"].SetId("TRACK", TrackId);
                file["RACE"].SetId("CONFIG_TRACK", TrackConfiguration);
            }
        }
    }
}