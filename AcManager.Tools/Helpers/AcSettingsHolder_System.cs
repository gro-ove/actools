using System.Linq;
using System.Windows.Media;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class SystemSettings : IniSettings {
            internal SystemSettings() : base("assetto_corsa", systemConfig: true) { }

            public SettingEntry[] ScreenshotFormats { get; } = {
                new SettingEntry("JPG", "JPEG"),
                new SettingEntry("BMP", "Bitmap (without compression)")
            };

            #region Some controls stuff
            private bool _softLock;

            public bool SoftLock {
                get { return _softLock; }
                set {
                    if (Equals(value, _softLock)) return;
                    _softLock = value;
                    OnPropertyChanged();
                }
            }

            private int _ffbSkipSteps;

            public int FfbSkipSteps {
                get { return _ffbSkipSteps; }
                set {
                    value = value.Clamp(0, 1000);
                    if (Equals(value, _ffbSkipSteps)) return;
                    _ffbSkipSteps = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Experimental FFB
            private bool _ffbGyro;

            public bool FfbGyro {
                get { return _ffbGyro; }
                set {
                    if (Equals(value, _ffbGyro)) return;
                    _ffbGyro = value;
                    OnPropertyChanged();
                }
            }

            private int _ffbDamperMinLevel;

            public int FfbDamperMinLevel {
                get { return _ffbDamperMinLevel; }
                set {
                    value = value.Clamp(0, 100);
                    if (Equals(value, _ffbDamperMinLevel)) return;
                    _ffbDamperMinLevel = value;
                    OnPropertyChanged();
                }
            }

            private int _ffbDamperGain;

            public int FfbDamperGain {
                get { return _ffbDamperGain; }
                set {
                    value = value.Clamp(0, 100);
                    if (Equals(value, _ffbDamperGain)) return;
                    _ffbDamperGain = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Miscellaneous
            private int _simulationValue;

            public int SimulationValue {
                get { return _simulationValue; }
                set {
                    value = value.Clamp(0, 100);
                    if (Equals(value, _simulationValue)) return;
                    _simulationValue = value;
                    OnPropertyChanged();
                }
            }

            private bool _developerApps;

            public bool DeveloperApps {
                get { return _developerApps; }
                set {
                    if (Equals(value, _developerApps)) return;
                    _developerApps = value;
                    OnPropertyChanged();
                }
            }

            private bool _allowFreeCamera;

            public bool AllowFreeCamera {
                get { return _allowFreeCamera; }
                set {
                    if (Equals(value, _allowFreeCamera)) return;
                    _allowFreeCamera = value;
                    OnPropertyChanged();
                }
            }

            private bool _logging;

            public bool Logging {
                get { return _logging; }
                set {
                    if (Equals(value, _logging)) return;
                    _logging = value;
                    OnPropertyChanged();
                }
            }

            private SettingEntry _screenshotFormat;

            public SettingEntry ScreenshotFormat {
                get { return _screenshotFormat; }
                set {
                    if (!ScreenshotFormats.Contains(value)) value = ScreenshotFormats[0];
                    if (Equals(value, _screenshotFormat)) return;
                    _screenshotFormat = value;
                    OnPropertyChanged();
                }
            }

            private int _mirrorsFieldOfView;

            public int MirrorsFieldOfView {
                get { return _mirrorsFieldOfView; }
                set {
                    value = value.Clamp(1, 180);
                    if (Equals(value, _mirrorsFieldOfView)) return;
                    _mirrorsFieldOfView = value;
                    OnPropertyChanged();
                }
            }

            private int _mirrorsFarPlane;

            public int MirrorsFarPlane {
                get { return _mirrorsFarPlane; }
                set {
                    value = value.Clamp(10, 2000);
                    if (Equals(value, _mirrorsFarPlane)) return;
                    _mirrorsFarPlane = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            public void LoadFfbFromIni(IniFile ini) {
                SoftLock = ini["SOFT_LOCK"].GetBool("ENABLED", false);
                FfbSkipSteps = ini["FORCE_FEEDBACK"].GetInt("FF_SKIP_STEPS", 1);

                var section = ini["FF_EXPERIMENTAL"];
                FfbGyro = section.GetBool("ENABLE_GYRO", false);
                FfbDamperMinLevel = section.GetDouble("DAMPER_MIN_LEVEL", 0d).ToIntPercentage();
                FfbDamperGain = section.GetDouble("DAMPER_GAIN", 1d).ToIntPercentage();
            }

            public void SaveFfbToIni(IniFile ini) {
                ini["SOFT_LOCK"].Set("ENABLED", SoftLock);
                ini["FORCE_FEEDBACK"].Set("FF_SKIP_STEPS", FfbSkipSteps);

                var section = ini["FF_EXPERIMENTAL"];
                section.Set("ENABLE_GYRO", FfbGyro);
                section.Set("DAMPER_MIN_LEVEL", FfbDamperMinLevel.ToDoublePercentage());
                section.Set("DAMPER_GAIN", FfbDamperGain.ToDoublePercentage());
            }

            protected override void LoadFromIni() {
                LoadFfbFromIni(Ini);

                SimulationValue = Ini["ASSETTO_CORSA"].GetDouble("SIMULATION_VALUE", 0d).ToIntPercentage();
                DeveloperApps = Ini["AC_APPS"].GetBool("ENABLE_DEV_APPS", false);
                AllowFreeCamera = Ini["CAMERA"].GetBool("ALLOW_FREE_CAMERA", false);
                Logging = !Ini["LOG"].GetBool("SUPPRESS", false);
                ScreenshotFormat = Ini["SCREENSHOT"].GetEntry("FORMAT", ScreenshotFormats);
                MirrorsFieldOfView = Ini["MIRRORS"].GetInt("FOV", 10);
                MirrorsFarPlane = Ini["MIRRORS"].GetInt("FAR_PLANE", 400);
            }

            protected override void SetToIni() {
                SaveFfbToIni(Ini);

                Ini["ASSETTO_CORSA"].Set("SIMULATION_VALUE", SimulationValue.ToDoublePercentage());
                Ini["AC_APPS"].Set("ENABLE_DEV_APPS", DeveloperApps);
                Ini["CAMERA"].Set("ALLOW_FREE_CAMERA", AllowFreeCamera);
                Ini["LOG"].Set("SUPPRESS", !Logging);
                Ini["SCREENSHOT"].Set("FORMAT", ScreenshotFormat);
                Ini["MIRRORS"].Set("FOV", MirrorsFieldOfView);
                Ini["MIRRORS"].Set("FAR_PLANE", MirrorsFarPlane);
            }
        }

        private static SystemSettings _system;

        public static SystemSettings System => _system ?? (_system = new SystemSettings());

        public class ProximityIndicatorSettings : IniSettings {
            internal ProximityIndicatorSettings() : base("proximity_indicator", systemConfig: true) { }

            private bool _isEnabled;

            public bool IsEnabled {
                get { return _isEnabled; }
                set {
                    if (Equals(value, _isEnabled)) return;
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                IsEnabled = !Ini["SETTINGS"].GetBool("HIDE", false);
            }

            protected override void SetToIni() {
                Ini["SETTINGS"].Set("HIDE", !IsEnabled);
            }
        }

        private static ProximityIndicatorSettings _proximityIndicator;

        public static ProximityIndicatorSettings ProximityIndicator => _proximityIndicator ?? (_proximityIndicator = new ProximityIndicatorSettings());

        public class GhostSettings : IniSettings {
            internal GhostSettings() : base("ghost_car", systemConfig: true) {}

            private Color _color;

            public Color Color {
                get { return _color; }
                set {
                    if (Equals(value, _color)) return;
                    _color = value;
                    OnPropertyChanged();
                }
            }

            private int _maxMinutesRecording;

            public int MaxMinutesRecording {
                get { return _maxMinutesRecording; }
                set {
                    value = value.Clamp(0, 240);
                    if (Equals(value, _maxMinutesRecording)) return;
                    _maxMinutesRecording = value;
                    OnPropertyChanged();
                }
            }

            private int _minDistance;

            public int MinDistance {
                get { return _minDistance; }
                set {
                    value = value.Clamp(0, 250);
                    if (Equals(value, _minDistance)) return;
                    _minDistance = value;
                    OnPropertyChanged();

                    if (value > MaxDistance) {
                        MaxDistance = value;
                    }
                }
            }

            private int _maxDistance;

            public int MaxDistance {
                get { return _maxDistance; }
                set {
                    value = value.Clamp(0, 500);
                    if (Equals(value, _maxDistance)) return;
                    _maxDistance = value;
                    OnPropertyChanged();

                    if (value < MinDistance) {
                        MinDistance = value;
                    }
                }
            }

            private int _maxOpacity;

            public int MaxOpacity {
                get { return _maxOpacity; }
                set {
                    value = value.Clamp(0, 100);
                    if (Equals(value, _maxOpacity)) return;
                    _maxOpacity = value;
                    OnPropertyChanged();
                }
            }

            private bool _timeDifferenceEnabled;

            public bool TimeDifferenceEnabled {
                get { return _timeDifferenceEnabled; }
                set {
                    if (Equals(value, _timeDifferenceEnabled)) return;
                    _timeDifferenceEnabled = value;
                    OnPropertyChanged();
                }
            }

            private bool _playerNameEnabled;

            public bool PlayerNameEnabled {
                get { return _playerNameEnabled; }
                set {
                    if (Equals(value, _playerNameEnabled)) return;
                    _playerNameEnabled = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                var section = Ini["GHOST_CAR"];
                Color = section.GetColor("COLOR", Color.FromRgb(150, 150, 255));
                MaxMinutesRecording = section.GetInt("MAX_MINUTES_RECORDING", 20);
                MinDistance = section.GetInt("MIN_DISTANCE", 10);
                MaxDistance = section.GetInt("MAX_DISTANCE", 50);
                MaxOpacity = section.GetDouble("MAX_OPACITY", 0.25).ToIntPercentage();
                TimeDifferenceEnabled = section.GetBool("TIME_DIFF_ENABLED", true);
                PlayerNameEnabled = section.GetBool("PLAYER_NAME_ENABLED", true);
            }

            protected override void SetToIni() {
                var section = Ini["GHOST_CAR"];
                section.Set("COLOR", Color);
                section.Set("MAX_MINUTES_RECORDING", MaxMinutesRecording);
                section.Set("MIN_DISTANCE", MinDistance);
                section.Set("MAX_DISTANCE", MaxDistance);
                section.Set("MAX_OPACITY", MaxOpacity.ToDoublePercentage());
                section.Set("TIME_DIFF_ENABLED", TimeDifferenceEnabled);
                section.Set("PLAYER_NAME_ENABLED", PlayerNameEnabled);
            }
        }

        private static GhostSettings _ghost;

        public static GhostSettings Ghost => _ghost ?? (_ghost = new GhostSettings());
    }
}
