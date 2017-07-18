using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class AudioSettings : IniPresetableSettings {
        /*public SettingEntry[] Latencies { get; } = {
            new SettingEntry(0, ToolsStrings.AcSettings_Quality_Normal),
            new SettingEntry(1, ToolsStrings.AcSettings_Quality_Low),
            new SettingEntry(2, ToolsStrings.AcSettings_Quality_VeryLow)
        };*/

        internal AudioSettings() : base("audio") { }

        /*private SettingEntry _latency;

        public SettingEntry Latency {
            get { return _latency; }
            set {
                if (!Latencies.Contains(value)) value = Latencies[0];
                if (Equals(value, _latency)) return;
                _latency = value;
                OnPropertyChanged();
            }
        }*/

        private int _skidsEntryPoint;

        public int SkidsEntryPoint {
            get { return _skidsEntryPoint; }
            set {
                value = value.Clamp(0, 200);
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
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelMaster)) return;
                _levelMaster = value;
                OnPropertyChanged();
            }
        }

        private double _levelTyres;

        public double LevelTyres {
            get { return _levelTyres; }
            set {
                value = value.Clamp(0d, 100d);
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
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelBrakes)) return;
                _levelBrakes = value;
                OnPropertyChanged();
            }
        }

        private double _levelDirtBottom;

        public double LevelDirtBottom {
            get { return _levelDirtBottom; }
            set {
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelDirtBottom)) return;
                _levelDirtBottom = value;
                OnPropertyChanged();
            }
        }

        public double LevelEngine {
            get { return _levelEngine; }
            set {
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelEngine)) return;
                _levelEngine = value;
                OnPropertyChanged();
            }
        }

        private double _levelSurfaces;

        public double LevelSurfaces {
            get { return _levelSurfaces; }
            set {
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelSurfaces)) return;
                _levelSurfaces = value;
                OnPropertyChanged();
            }
        }

        private double _levelWind;

        public double LevelWind {
            get { return _levelWind; }
            set {
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelWind)) return;
                _levelWind = value;
                OnPropertyChanged();
            }
        }

        private double _levelOpponents;

        public double LevelOpponents {
            get { return _levelOpponents; }
            set {
                value = value.Clamp(0d, 100d);
                if (Equals(value, _levelOpponents)) return;
                _levelOpponents = value;
                OnPropertyChanged();
            }
        }

        private double _levelUi;

        public double LevelUi {
            get { return _levelUi; }
            set {
                value = value.Clamp(0d, 100d);
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

            // Latency = Ini["SETTINGS"].GetEntry("LATENCY", Latencies, 1);
            SkidsEntryPoint = Ini["SKIDS"].GetInt("ENTRY_POINT", 100);
        }
        
        protected override void SetToIni(IniFile ini) {
            ini["LEVELS"].Set("MASTER", LevelMaster / 100d);
            ini["LEVELS"].Set("TYRES", LevelTyres / 100d);
            ini["LEVELS"].Set("BRAKES", LevelBrakes / 100d);
            ini["LEVELS"].Set("ENGINE", LevelEngine / 100d);
            ini["LEVELS"].Set("SURFACES", LevelSurfaces / 100d);
            ini["LEVELS"].Set("WIND", LevelWind / 100d);
            ini["LEVELS"].Set("OPPONENTS", LevelOpponents / 100d);
            ini["LEVELS"].Set("DIRT_BOTTOM", LevelDirtBottom / 100d);
            ini["LEVELS"].Set("UISOUNDS", LevelUi / 100d);

            // Ini["SETTINGS"].Set("LATENCY", Latency);
            ini["SKIDS"].Set("ENTRY_POINT", SkidsEntryPoint);
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.AudioPresetChanged();
        }
    }
}