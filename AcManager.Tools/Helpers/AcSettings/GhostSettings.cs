using System.Windows.Media;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
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
}