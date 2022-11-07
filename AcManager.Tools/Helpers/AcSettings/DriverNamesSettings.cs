using System.Windows.Media;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettings {
    public class DriverNamesSettings : IniSettings {
        internal DriverNamesSettings() : base("name_displayer", systemConfig: true) { }

        public SettingEntry[] Modes { get; } = {
            new SettingEntry("simple", "Simple label"),
            new SettingEntry("withFlag", "With flag"),
            new SettingEntry("withoutFlag", "Without flag"),
        };

        private SettingEntry _mode;

        public SettingEntry Mode {
            get => _mode;
            set => Apply(value.EnsureFrom(Modes), ref _mode);
        }

        private double _scale;

        public double Scale {
            get => _scale;
            set => Apply(value, ref _scale);
        }

        private Color _color;

        public Color Color {
            get => _color;
            set => Apply(value, ref _color);
        }

        private double _maxDistance;

        public double MaxDistance {
            get => _maxDistance;
            set => Apply(value, ref _maxDistance);
        }

        private bool _drawFocusedCar;

        public bool DrawFocusedCar {
            get => _drawFocusedCar;
            set => Apply(value, ref _drawFocusedCar);
        }

        protected override void LoadFromIni() {
            var section = Ini["MAIN"];
            MaxDistance = section.GetDouble("MAX_DISTANCE", 100d);
            Color = section.GetNormalizedColor("FONT_COLOR", Colors.White);
            DrawFocusedCar = section.GetBool("DRAW_FOCUSEDCAR", false);
            Scale = section.GetDouble("SCALE", 1d);
            Mode = section.GetBool("SIMPLE_LABEL", false) ? Modes.GetById("simple")
                    : section.GetBool("RENDER_FLAG", true) ? Modes.GetById("withFlag") : Modes.GetById("withoutFlag");
        }

        protected override void SetToIni() {
            var section = Ini["MAIN"];
            section.Set("MAX_DISTANCE", MaxDistance);
            section.SetNormalizedColor("FONT_COLOR", Color);
            section.Set("DRAW_FOCUSEDCAR", DrawFocusedCar);
            section.Set("SCALE", Scale);
            section.Set("SIMPLE_LABEL", Mode?.Id == "simple");
            section.Set("RENDER_FLAG", Mode?.Id == "withFlag");
        }
    }
}