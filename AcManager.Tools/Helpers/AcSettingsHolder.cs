using System;
using System.Linq;
using AcTools.Utils;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class ReplaySettings : IniSettings {
            public enum ReplayQuality {
                Minimum = 0,
                Low = 1,
                Medium = 2,
                High = 3,
                Ultra = 4
            }

            public ReplayQuality[] Qualities { get; } = {
                ReplayQuality.Minimum,
                ReplayQuality.Low,
                ReplayQuality.Medium,
                ReplayQuality.High,
                ReplayQuality.Ultra
            };

            internal ReplaySettings() : base("replay") { }

            private int _maxSize;

            public int MaxSize {
                get { return _maxSize; }
                set {
                    value = MathUtils.Clamp(value, 10, 2000);
                    if (Equals(value, _maxSize)) return;
                    _maxSize = value;
                    OnPropertyChanged();
                }
            }

            private static int? _recommendedSize;

            public int? RecommendedSize {
                get {
                    if (_recommendedSize != null) return _recommendedSize;

                    var memStatus = new Kernel32.MEMORYSTATUSEX();
                    if (!Kernel32.GlobalMemoryStatusEx(memStatus)) return null;

                    var installedMemory = memStatus.ullTotalPhys;
                    _recommendedSize = Math.Min((int)(0.1 * installedMemory / 1024 / 1024), 1000);
                    return _recommendedSize;
                }
            }

            private ReplayQuality _quality;

            public ReplayQuality Quality {
                get { return _quality; }
                set {
                    if (Equals(value, _quality)) return;
                    _quality = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                MaxSize = Ini["REPLAY"].GetInt("MAX_SIZE_MB", 200);
                Quality = Ini["QUALITY"].GetEnum("LEVEL", ReplayQuality.High);
            }

            protected override void SetToIni() {
                Ini["REPLAY"].Set("MAX_SIZE_MB", MaxSize);
                Ini["QUALITY"].Set("LEVEL", Quality);
            }
        }

        public static ReplaySettings Replay = new ReplaySettings();

        public class AudioSettings : IniSettings {
            public enum AudioLatency {
                Normal = 0,
                Low = 1,
                VeryLow = 2
            }

            public AudioLatency[] Latencies { get; } = {
                AudioLatency.Normal,
                AudioLatency.Low,
                AudioLatency.VeryLow
            };

            internal AudioSettings() : base("audio") { }

            private AudioLatency _latency;

            public AudioLatency Latency {
                get { return _latency; }
                set {
                    if (Equals(value, _latency)) return;
                    _latency = value;
                    OnPropertyChanged();
                }
            }

            private int _skidsEntryPoint;

            public int SkidsEntryPoint {
                get { return _skidsEntryPoint; }
                set {
                    value = MathUtils.Clamp(value, 0, 200);
                    if (Equals(value, _skidsEntryPoint)) return;
                    _skidsEntryPoint = value;
                    OnPropertyChanged();
                }
            }

            #region Levels
            private double _levelMaster;

            public double LevelMaster {
                get { return _levelMaster; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelMaster)) return;
                    _levelMaster = value;
                    OnPropertyChanged();
                }
            }

            private double _levelTyres;

            public double LevelTyres {
                get { return _levelTyres; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelTyres)) return;
                    _levelTyres = value;
                    OnPropertyChanged();
                }
            }

            private double _levelEngine;

            private double _levelBrakes;

            public double LevelBrakes {
                get { return _levelBrakes; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelBrakes)) return;
                    _levelBrakes = value;
                    OnPropertyChanged();
                }
            }

            private double _levelDirtBottom;

            public double LevelDirtBottom {
                get { return _levelDirtBottom; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelDirtBottom)) return;
                    _levelDirtBottom = value;
                    OnPropertyChanged();
                }
            }

            public double LevelEngine {
                get { return _levelEngine; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelEngine)) return;
                    _levelEngine = value;
                    OnPropertyChanged();
                }
            }

            private double _levelSurfaces;

            public double LevelSurfaces {
                get { return _levelSurfaces; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelSurfaces)) return;
                    _levelSurfaces = value;
                    OnPropertyChanged();
                }
            }

            private double _levelWind;

            public double LevelWind {
                get { return _levelWind; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelWind)) return;
                    _levelWind = value;
                    OnPropertyChanged();
                }
            }

            private double _levelOpponents;

            public double LevelOpponents {
                get { return _levelOpponents; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelOpponents)) return;
                    _levelOpponents = value;
                    OnPropertyChanged();
                }
            }

            private double _levelUi;

            public double LevelUi {
                get { return _levelUi; }
                set {
                    value = MathUtils.Clamp(value, 0d, 100d);
                    if (Equals(value, _levelUi)) return;
                    _levelUi = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            protected override void LoadFromIni() {
                LevelMaster = Ini["LEVELS"].GetDouble("MASTER", 1.0) * 100d;
                LevelTyres = Ini["LEVELS"].GetDouble("TYRES", 0.8) * 100d;
                LevelBrakes = Ini["LEVELS"].GetDouble("BRAKES", 0.8) * 100d;
                LevelEngine = Ini["LEVELS"].GetDouble("ENGINE", 1.0) * 100d;
                LevelSurfaces = Ini["LEVELS"].GetDouble("SURFACES", 1.0) * 100d;
                LevelWind = Ini["LEVELS"].GetDouble("WIND", 0.9) * 100d;
                LevelOpponents = Ini["LEVELS"].GetDouble("OPPONENTS", 0.9) * 100d;
                LevelDirtBottom = Ini["LEVELS"].GetDouble("DIRT_BOTTOM", 1.0) * 100d;
                LevelUi = Ini["LEVELS"].GetDouble("UISOUNDS", 0.7) * 100d;

                Latency = Ini["SETTINGS"].GetEnum("LATENCY", AudioLatency.Low);
                SkidsEntryPoint = Ini["SKIDS"].GetInt("ENTRY_POINT", 100);
            }

            protected override void SetToIni() {
                Ini["LEVELS"].Set("MASTER", LevelMaster / 100d);
                Ini["LEVELS"].Set("TYRES", LevelTyres / 100d);
                Ini["LEVELS"].Set("BRAKES", LevelBrakes / 100d);
                Ini["LEVELS"].Set("ENGINE", LevelEngine / 100d);
                Ini["LEVELS"].Set("SURFACES", LevelSurfaces / 100d);
                Ini["LEVELS"].Set("WIND", LevelWind / 100d);
                Ini["LEVELS"].Set("OPPONENTS", LevelOpponents / 100d);
                Ini["LEVELS"].Set("DIRT_BOTTOM", LevelDirtBottom / 100d);
                Ini["LEVELS"].Set("UISOUNDS", LevelUi / 100d);

                Ini["SETTINGS"].Set("LATENCY", Latency);
                Ini["SKIDS"].Set("ENTRY_POINT", SkidsEntryPoint);
            }
        }

        public static AudioSettings Audio = new AudioSettings();

        public class CameraOnboardSettings : IniSettings {
            internal CameraOnboardSettings() : base("camera_onboard") {}

            private int _fieldOfView;

            public int FieldOfView {
                get { return _fieldOfView; }
                set {
                    value = MathUtils.Clamp(value, 10, 120);
                    if (Equals(value, _fieldOfView)) return;
                    _fieldOfView = value;
                    OnPropertyChanged();
                }
            }

            private bool _worldAligned;

            public bool WorldAligned {
                get { return _worldAligned; }
                set {
                    if (Equals(value, _worldAligned)) return;
                    _worldAligned = value;
                    OnPropertyChanged();
                }
            }

            private int _glancingSpeed;

            public int GlancingSpeed {
                get { return _glancingSpeed; }
                set {
                    value = MathUtils.Clamp(value, 1, 40);
                    if (Equals(value, _glancingSpeed)) return;
                    _glancingSpeed = value;
                    OnPropertyChanged();
                }
            }

            private int _glancingAngle;

            public int GlancingAngle {
                get { return _glancingAngle; }
                set {
                    value = MathUtils.Clamp(value, 5, 90);
                    if (Equals(value, _glancingAngle)) return;
                    _glancingAngle = value;
                    OnPropertyChanged();
                }
            }

            public bool GForcesBinded {
                get { return ValuesStorage.GetBool("AcSettings.CameraOnboard.GForceBinded", true); }
                set {
                    if (Equals(value, GForcesBinded)) return;
                    ValuesStorage.Set("AcSettings.CameraOnboard.GForceBinded", value);
                    OnPropertyChanged(false);
                    
                    if (value) {
                        GForceY = GForceX;
                        GForceZ = GForceX;
                    }
                }
            }

            private int _gForceX;

            public int GForceX {
                get { return _gForceX; }
                set {
                    value = MathUtils.Clamp(value, 0, 300);
                    if (Equals(value, _gForceX)) return;
                    _gForceX = value;
                    OnPropertyChanged();

                    if (GForcesBinded) {
                        GForceY = GForceX;
                        GForceZ = GForceX;
                    }
                }
            }

            private int _gForceY;

            public int GForceY {
                get { return _gForceY; }
                set {
                    value = MathUtils.Clamp(value, 0, 300);
                    if (Equals(value, _gForceY)) return;
                    _gForceY = value;
                    OnPropertyChanged();
                }
            }

            private int _gForceZ;

            public int GForceZ {
                get { return _gForceZ; }
                set {
                    value = MathUtils.Clamp(value, 0, 300);
                    if (Equals(value, _gForceZ)) return;
                    _gForceZ = value;
                    OnPropertyChanged();
                }
            }

            private int _highSpeedShaking;

            public int HighSpeedShaking {
                get { return _highSpeedShaking; }
                set {
                    value = MathUtils.Clamp(value, 0, 200);
                    if (Equals(value, _highSpeedShaking)) return;
                    _highSpeedShaking = value;
                    OnPropertyChanged();
                }
            }

            private static readonly double[] DefaultShaking = { 0.001, 0.0002, 0.00015 };
            private static readonly double[] DefaultGForces = { -0.002, -0.0020, -0.0025 };

            protected override void LoadFromIni() {
                FieldOfView = Ini["MODE"].GetInt("FOV", 56);
                WorldAligned = Ini["MODE"].GetBool("IS_WORLD_ALIGNED", false);
                GlancingSpeed = Ini["ROTATION"].GetInt("SPEED", 20);
                GlancingAngle = Ini["ROTATION"].GetInt("HEAD_MAX_DEGREES", 60);

                var shaking = Ini["SHAKE"].GetVector3("SCALE");
                HighSpeedShaking = (int)(100 * shaking[0] / DefaultShaking[0]);
                
                var gForces = Ini["GFORCES"].GetVector3("MIX");
                var gForceX = (int)(100 * gForces[0] / DefaultGForces[0]);
                var gForceY = (int)(100 * gForces[1] / DefaultGForces[1]);
                var gForceZ = (int)(100 * gForces[2] / DefaultGForces[2]);

                if (gForceX != gForceY || gForceX != gForceZ) {
                    GForcesBinded = false;
                }

                GForceX = gForceX;
                GForceY = gForceY;
                GForceZ = gForceZ;
            }

            protected override void SetToIni() {
                Ini["MODE"].Set("FOV", FieldOfView);
                Ini["MODE"].Set("IS_WORLD_ALIGNED", WorldAligned);
                Ini["ROTATION"].Set("SPEED", GlancingSpeed);
                Ini["ROTATION"].Set("HEAD_MAX_DEGREES", GlancingAngle);

                Ini["SHAKE"].Set("SCALE", DefaultShaking.Select(x => x * HighSpeedShaking / 100));
                Ini["GFORCES"].Set("MIX", GForcesBinded ? DefaultGForces.Select(x => x * GForceX / 100)
                        : new[] {
                            DefaultGForces[0] * GForceX / 100,
                            DefaultGForces[1] * GForceY / 100,
                            DefaultGForces[2] * GForceZ / 100
                        });
            }
        }

        public static CameraOnboardSettings CameraOnboard = new CameraOnboardSettings();

        public class GameplaySettings : IniSettings {
            public SettingEntry[] UnitsTypes { get; } = {
                new SettingEntry("0", "Metrical (km/h)"),
                new SettingEntry("1", "Imperial (mph)")
            };

            internal GameplaySettings() : base("gameplay") {}

            #region GUI
            private SettingEntry _units;

            public SettingEntry Units {
                get { return _units; }
                set {
                    if (!UnitsTypes.Contains(value)) value = UnitsTypes[0];
                    if (Equals(value, _units)) return;
                    _units = value;
                    OnPropertyChanged();
                }
            }

            private bool _allowOverlapping;

            public bool AllowOverlapping {
                get { return _allowOverlapping; }
                set {
                    if (Equals(value, _allowOverlapping)) return;
                    _allowOverlapping = value;
                    OnPropertyChanged();
                }
            }

            private bool _displayTimeGap;

            public bool DisplayTimeGap {
                get { return _displayTimeGap; }
                set {
                    if (Equals(value, _displayTimeGap)) return;
                    _displayTimeGap = value;
                    OnPropertyChanged();
                }
            }

            private bool _displayDamage;

            public bool DisplayDamage {
                get { return _displayDamage; }
                set {
                    if (Equals(value, _displayDamage)) return;
                    _displayDamage = value;
                    OnPropertyChanged();
                }
            }

            private bool _displayLeaderboard;

            public bool DisplayLeaderboard {
                get { return _displayLeaderboard; }
                set {
                    if (Equals(value, _displayLeaderboard)) return;
                    _displayLeaderboard = value;
                    OnPropertyChanged();
                }
            }

            private bool _displayMirror;

            public bool DisplayMirror {
                get { return _displayMirror; }
                set {
                    if (Equals(value, _displayMirror)) return;
                    _displayMirror = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            private int _steeringWheelLimit;

            public int SteeringWheelLimit {
                get { return _steeringWheelLimit; }
                set {
                    value = MathUtils.Clamp(value, 0, 450);
                    if (Equals(value, _steeringWheelLimit)) return;
                    _steeringWheelLimit = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                Units = Ini["OPTIONS"].GetEntry("USE_MPH", UnitsTypes);
                DisplayTimeGap = Ini["TIME_DIFFERENCE"].GetBool("IS_ACTIVE", true);
                DisplayDamage = Ini["DAMAGE_DISPLAYER"].GetBool("IS_ACTIVE", true);
                DisplayLeaderboard = Ini["OVERLAY_LEADERBOARD"].GetBool("ACTIVE", true);
                DisplayMirror = Ini["VIRTUAL_MIRROR"].GetBool("ACTIVE", true);
                AllowOverlapping = Ini["GUI"].GetBool("ALLOW_OVERLAPPING_FORMS", true);
                SteeringWheelLimit = Ini["STEER_ANIMATION"].GetInt("MAX_DEGREES", 0);
            }

            protected override void SetToIni() {
                Ini["OPTIONS"].Set("USE_MPH", Units);
                Ini["TIME_DIFFERENCE"].Set("IS_ACTIVE", DisplayTimeGap);
                Ini["DAMAGE_DISPLAYER"].Set("IS_ACTIVE", DisplayDamage);
                Ini["OVERLAY_LEADERBOARD"].Set("ACTIVE", DisplayLeaderboard);
                Ini["VIRTUAL_MIRROR"].Set("ACTIVE", DisplayMirror);
                Ini["GUI"].Set("ALLOW_OVERLAPPING_FORMS", AllowOverlapping);
                Ini["STEER_ANIMATION"].Set("MAX_DEGREES", SteeringWheelLimit);
            }
        }

        public static GameplaySettings Gameplay = new GameplaySettings();
    }
}
