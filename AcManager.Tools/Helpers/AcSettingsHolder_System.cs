using System.Linq;
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
            #endregion
            
            public void LoadFfbFromIni(IniFile ini) {
                var section = ini["FF_EXPERIMENTAL"];
                FfbGyro = section.GetBool("ENABLE_GYRO", false);
                FfbDamperMinLevel = section.GetDouble("DAMPER_MIN_LEVEL", 0d).ToIntPercentage();
                FfbDamperGain = section.GetDouble("DAMPER_GAIN", 1d).ToIntPercentage();
            }

            public void SaveFfbToIni(IniFile ini) {
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
            }

            protected override void SetToIni() {
                SaveFfbToIni(Ini);

                Ini["ASSETTO_CORSA"].Set("SIMULATION_VALUE", SimulationValue.ToDoublePercentage());
                Ini["AC_APPS"].Set("ENABLE_DEV_APPS", DeveloperApps);
                Ini["CAMERA"].Set("ALLOW_FREE_CAMERA", AllowFreeCamera);
                Ini["LOG"].Set("SUPPRESS", !Logging);
                Ini["SCREENSHOT"].Set("FORMAT", ScreenshotFormat);
            }
        }

        private static SystemSettings _system;

        public static SystemSettings System => _system ?? (_system = new SystemSettings());

        public class ProximityIndicatorSettings : IniSettings {
            internal ProximityIndicatorSettings() : base("proximity_indicator", systemConfig: true) { }

            private bool _enable;

            public bool Enable {
                get { return _enable; }
                set {
                    if (Equals(value, _enable)) return;
                    _enable = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                Enable = !Ini["SETTINGS"].GetBool("HIDE", false);
            }

            protected override void SetToIni() {
                Ini["SETTINGS"].Set("HIDE", !Enable);
            }
        }

        private static ProximityIndicatorSettings _proximityIndicator;

        public static ProximityIndicatorSettings ProximityIndicator => _proximityIndicator ?? (_proximityIndicator = new ProximityIndicatorSettings());
    }
}
