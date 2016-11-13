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
            get { return _maxSize; }
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

                var memStatus = new Kernel32.MEMORYSTATUSEX();
                if (!Kernel32.GlobalMemoryStatusEx(memStatus)) return null;

                var installedMemory = memStatus.ullTotalPhys;
                _recommendedSize = Math.Min((int)(0.1 * installedMemory / 1024 / 1024), 1000);
                return _recommendedSize;
            }
        }

        private SettingEntry _quality;

        public SettingEntry Quality {
            get { return _quality; }
            set {
                if (!Qualities.Contains(value)) value = Qualities[0];
                if (Equals(value, _quality)) return;
                _quality = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            MaxSize = Ini["REPLAY"].GetInt("MAX_SIZE_MB", 200);
            Quality = Ini["QUALITY"].GetEntry("LEVEL", Qualities, 3);
        }

        protected override void SetToIni() {
            Ini["REPLAY"].Set("MAX_SIZE_MB", MaxSize);
            Ini["QUALITY"].Set("LEVEL", Quality);
        }
    }
}
