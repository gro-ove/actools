using System;
using System.Linq;
using AcTools.Utils;
using AcTools.Windows;

namespace AcManager.Tools.Helpers.AcSettings {
    public class ReplaySettings : IniSettings {
        public SettingEntry[] Qualities { get; } = {
            new SettingEntry(0, ToolsStrings.AcSettings_Quality_Minimum),
            new SettingEntry(1, ToolsStrings.AcSettings_Quality_Low),
            new SettingEntry(2, ToolsStrings.AcSettings_Quality_Medium),
            new SettingEntry(3, ToolsStrings.AcSettings_Quality_High),
            new SettingEntry(4, ToolsStrings.AcSettings_Quality_Ultra)
        };

        internal ReplaySettings() : base("replay") {}

        private int _maxSize;

        public int MaxSize {
            get => _maxSize;
            set {
                value = value.Clamp(10, 1000);
                if (Equals(value, _maxSize)) return;
                _maxSize = value;
                OnPropertyChanged();
            }
        }

        private static int? _recommendedSize;

        public int? RecommendedSize {
            get {
                if (_recommendedSize != null) return _recommendedSize;

                var memStatus = new Kernel32.MemoryStatusEx();
                if (!Kernel32.GlobalMemoryStatusEx(memStatus)) return null;

                var installedMemory = memStatus.Total;
                _recommendedSize = Math.Min((int)(0.1 * installedMemory / 1024 / 1024), 2000);
                return _recommendedSize;
            }
        }

        private SettingEntry _quality;

        public SettingEntry Quality {
            get => _quality;
            set {
                if (!Qualities.Contains(value)) value = Qualities[0];
                if (Equals(value, _quality)) return;
                _quality = value;
                OnPropertyChanged();
            }
        }

        private bool _autosave;

        public bool Autosave {
            get => _autosave;
            set {
                if (Equals(value, _autosave)) return;
                _autosave = value;
                OnPropertyChanged();
            }
        }

        private int _racesLimit;

        public int RacesLimit {
            get => _racesLimit;
            set {
                if (Equals(value, _racesLimit)) return;
                _racesLimit = value;
                OnPropertyChanged();
            }
        }

        private int _qualifyLimit;

        public int QualifyLimit {
            get => _qualifyLimit;
            set {
                if (Equals(value, _qualifyLimit)) return;
                _qualifyLimit = value;
                OnPropertyChanged();
            }
        }

        private int _othersLimit;

        public int OthersLimit {
            get => _othersLimit;
            set {
                if (Equals(value, _othersLimit)) return;
                _othersLimit = value;
                OnPropertyChanged();
            }
        }

        private int _minTimeSecond;

        public int MinTimeSecond {
            get => _minTimeSecond;
            set {
                value = Math.Max(0, value);
                if (Equals(value, _minTimeSecond)) return;
                _minTimeSecond = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            MaxSize = Ini["REPLAY"].GetInt("MAX_SIZE_MB", 200);
            Quality = Ini["QUALITY"].GetEntry("LEVEL", Qualities, 3);

            var autosave = Ini["AUTOSAVE"];
            Autosave = autosave.GetBool("ENABLED", true);
            RacesLimit = autosave.GetInt("RACE", 2);
            QualifyLimit = autosave.GetInt("QUALIFY", 1);
            OthersLimit = autosave.GetInt("OTHERS", 1);
            MinTimeSecond = autosave.GetInt("MIN_TIME_SECONDS", 30);
        }

        protected override void SetToIni() {
            Ini["REPLAY"].Set("MAX_SIZE_MB", MaxSize);
            Ini["QUALITY"].Set("LEVEL", Quality);

            var autosave = Ini["AUTOSAVE"];
            autosave.Set("ENABLED", Autosave);
            autosave.Set("RACE", RacesLimit);
            autosave.Set("QUALIFY", QualifyLimit);
            autosave.Set("OTHERS", OthersLimit);
            autosave.Set("MIN_TIME_SECONDS", MinTimeSecond);
        }
    }
}
