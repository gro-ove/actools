using System.Windows.Media;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class GhostSettings : IniSettings {
        internal GhostSettings() : base("ghost_car", systemConfig: true) {}

        private Color _color;

        public Color Color {
            get => _color;
            set => Apply(value, ref _color);
        }

        public int MaxMinutesRecordingDefault => 20;

        private int _maxMinutesRecording;

        public int MaxMinutesRecording {
            get => _maxMinutesRecording;
            set {
                value = value.Clamp(0, 240);
                if (Equals(value, _maxMinutesRecording)) return;
                _maxMinutesRecording = value;
                OnPropertyChanged();
            }
        }

        public int MinDistanceDefault => 10;

        private int _minDistance;

        public int MinDistance {
            get => _minDistance;
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

        public int MaxDistanceDefault => 50;

        private int _maxDistance;

        public int MaxDistance {
            get => _maxDistance;
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

        public int MaxOpacityDefault => 25;

        private int _maxOpacity;

        public int MaxOpacity {
            get => _maxOpacity;
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _maxOpacity)) return;
                _maxOpacity = value;
                OnPropertyChanged();
            }
        }

        private bool _timeDifferenceEnabled;

        public bool TimeDifferenceEnabled {
            get => _timeDifferenceEnabled;
            set => Apply(value, ref _timeDifferenceEnabled);
        }

        private bool _playerNameEnabled;

        public bool PlayerNameEnabled {
            get => _playerNameEnabled;
            set => Apply(value, ref _playerNameEnabled);
        }

        protected override void LoadFromIni() {
            var section = Ini["GHOST_CAR"];
            Color = section.GetColor("COLOR", Color.FromRgb(150, 150, 255));
            MaxMinutesRecording = section.GetInt("MAX_MINUTES_RECORDING", MaxMinutesRecordingDefault);
            MinDistance = section.GetInt("MIN_DISTANCE", MinDistanceDefault);
            MaxDistance = section.GetInt("MAX_DISTANCE", MaxDistanceDefault);
            MaxOpacity = section.GetDouble("MAX_OPACITY", MaxOpacityDefault.ToDoublePercentage()).ToIntPercentage();
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
}