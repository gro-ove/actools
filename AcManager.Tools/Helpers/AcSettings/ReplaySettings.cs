using System;
using System.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;

namespace AcManager.Tools.Helpers.AcSettings {
    public class ReplaySettings : IniSettings {
        public static int GetQualityFrequency(int qualityValue) {
            return new[] { 8, 12, 16, 33, 67 }.ElementAtOr(qualityValue, 16);
        }

        public SettingEntry[] Qualities { get; } = {
            new SettingEntry(0, $"{ToolsStrings.AcSettings_Quality_Minimum} ({GetQualityFrequency(0)} Hz)"),
            new SettingEntry(1, $"{ToolsStrings.AcSettings_Quality_Low} ({GetQualityFrequency(1)} Hz)"),
            new SettingEntry(2, $"{ToolsStrings.AcSettings_Quality_Medium} ({GetQualityFrequency(2)} Hz)"),
            new SettingEntry(3, $"{ToolsStrings.AcSettings_Quality_High} ({GetQualityFrequency(3)} Hz)"),
            new SettingEntry(4, $"{ToolsStrings.AcSettings_Quality_Ultra} ({GetQualityFrequency(4)} Hz)")
        };

        internal ReplaySettings() : base("replay") {}

        private int _maxSize;

        public int MaxSize {
            get => _maxSize;
            set {
                value = value.Clamp(10, 8000);
                if (Equals(value, _maxSize)) return;
                _maxSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstimatedDuration));
            }
        }

        public int MaxSizeMaximum => Math.Max(1000, RecommendedSize ?? 1000);

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

        public const int BytesPerCarPerEntry = 288;
        public const int BytesPerMovablePerEntry = 24;
        public const int CarsToEstimate = 24;
        public const int MovablesToEstimate = 12;

        public TimeSpan EstimatedDuration {
            get {
                var entriesPerSecond = GetQualityFrequency(Quality.IntValue ?? 2);
                var dataPerSecond = entriesPerSecond *
                        (BytesPerCarPerEntry * CarsToEstimate + BytesPerMovablePerEntry * MovablesToEstimate);
                return TimeSpan.FromSeconds((double)MaxSize * 1024 * 1024 / dataPerSecond);
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
                OnPropertyChanged(nameof(EstimatedDuration));
            }
        }

        private bool _autosave;

        public bool Autosave {
            get => _autosave;
            set => Apply(value, ref _autosave);
        }

        private int _racesLimit;

        public int RacesLimit {
            get => _racesLimit;
            set => Apply(value, ref _racesLimit);
        }

        private int _qualifyLimit;

        public int QualifyLimit {
            get => _qualifyLimit;
            set => Apply(value, ref _qualifyLimit);
        }

        private int _othersLimit;

        public int OthersLimit {
            get => _othersLimit;
            set => Apply(value, ref _othersLimit);
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
