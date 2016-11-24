using System.Windows.Media;

namespace AcManager.Tools.Helpers.AcSettings {
    public class SessionInfoSettings : IniSettings {
        internal SessionInfoSettings() : base("session_info", systemConfig: true) {}

        private bool _practice;

        public bool Practice {
            get { return _practice; }
            set {
                if (Equals(value, _practice)) return;
                _practice = value;
                OnPropertyChanged();
            }
        }

        private bool _qualify;

        public bool Qualify {
            get { return _qualify; }
            set {
                if (Equals(value, _qualify)) return;
                _qualify = value;
                OnPropertyChanged();
            }
        }

        private bool _race;

        public bool Race {
            get { return _race; }
            set {
                if (Equals(value, _race)) return;
                _race = value;
                OnPropertyChanged();
            }
        }

        private Color _backgroundColor;

        public Color BackgroundColor {
            get { return _backgroundColor; }
            set {
                if (Equals(value, _backgroundColor)) return;
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }

        private double _backgroundOpacity;

        public double BackgroundOpacity {
            get { return _backgroundOpacity; }
            set {
                if (Equals(value, _backgroundOpacity)) return;
                _backgroundOpacity = value;
                OnPropertyChanged();
            }
        }

        private Color _foregroundColor;

        public Color ForegroundColor {
            get { return _foregroundColor; }
            set {
                if (Equals(value, _foregroundColor)) return;
                _foregroundColor = value;
                OnPropertyChanged();
            }
        }

        private double _foregroundOpacity;

        public double ForegroundOpacity {
            get { return _foregroundOpacity; }
            set {
                if (Equals(value, _foregroundOpacity)) return;
                _foregroundOpacity = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            var section = Ini["OPTIONS"];
            Practice = section.GetBool("PRATICE_VISIBLE", true);
            Qualify = section.GetBool("QUALIFY_VISIBLE", true);
            Race = section.GetBool("RACE_VISIBLE", true);

            double opacity;
            BackgroundColor = section.GetColor("BACKGROUND_COLOR", Colors.Black, 0.3, out opacity);
            BackgroundOpacity = opacity;
            
            ForegroundColor = section.GetColor("FONT_COLOR", Colors.White, 1.0, out opacity);
            ForegroundOpacity = opacity;
        }

        protected override void SetToIni() {
            var section = Ini["OPTIONS"];
            section.Set("PRATICE_VISIBLE", Practice);
            section.Set("QUALIFY_VISIBLE", Qualify);
            section.Set("RACE_VISIBLE", Race);
            section.Set("BACKGROUND_COLOR", BackgroundColor, BackgroundOpacity);
            section.Set("FONT_COLOR", ForegroundColor, ForegroundOpacity);
        }
    }
}