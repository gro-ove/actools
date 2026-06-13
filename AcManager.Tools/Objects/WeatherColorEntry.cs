using System.Windows.Media;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public sealed class WeatherColorEntry : Displayable {
        public string Id { get; }

        public string Sub { get; }

        public Color DefaultColor { get; }

        public double DefaultMultiplier { get; }

        public double Maximum { get; }

        public double Step { get; }

        public WeatherColorEntry(string id, string sub, string displayName, Color defaultColor, double defaultMultiplier, double maximum) {
            Id = id;
            Sub = sub;
            DefaultMultiplier = defaultMultiplier;
            Maximum = maximum;
            Step = maximum / 10d;
            DefaultColor = defaultColor;
            DisplayName = displayName;
        }

        private Color _color;

        public Color Color {
            get { return _color; }
            set => Apply(value, ref _color);
        }

        private double _multiplier;

        public double Multiplier {
            get { return _multiplier; }
            set {
                if (Equals(value, _multiplier)) return;
                _multiplier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MultiplierRounded));
            }
        }

        public double MultiplierRounded {
            get { return _multiplier; }
            set { Multiplier = value.Round(0.01); }
        }
    }
}