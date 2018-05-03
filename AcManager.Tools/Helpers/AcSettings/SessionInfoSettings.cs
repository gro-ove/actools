using System.Windows.Media;

namespace AcManager.Tools.Helpers.AcSettings {
    public class SessionInfoSettings : IniSettings {
        internal SessionInfoSettings() : base("session_info", systemConfig: true) {}

        private bool _practice;

        public bool Practice {
            get => _practice;
            set => Apply(value, ref _practice);
        }

        private bool _qualify;

        public bool Qualify {
            get => _qualify;
            set => Apply(value, ref _qualify);
        }

        private bool _race;

        public bool Race {
            get => _race;
            set => Apply(value, ref _race);
        }

        private Color _backgroundColor;

        public Color BackgroundColor {
            get => _backgroundColor;
            set => Apply(value, ref _backgroundColor);
        }

        private double _backgroundOpacity;

        public double BackgroundOpacity {
            get => _backgroundOpacity;
            set => Apply(value, ref _backgroundOpacity);
        }

        private Color _foregroundColor;

        public Color ForegroundColor {
            get => _foregroundColor;
            set => Apply(value, ref _foregroundColor);
        }

        private double _foregroundOpacity;

        public double ForegroundOpacity {
            get => _foregroundOpacity;
            set => Apply(value, ref _foregroundOpacity);
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