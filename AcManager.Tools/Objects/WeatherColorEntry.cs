using System.Windows.Media;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Objects {
    public sealed class WeatherColorEntry : Displayable {
        public string Id { get; }

        public string Sub { get; }

        public Color DefaultColor { get; }

        public double DefaultMultipler { get; }

        public double Maximum { get; }

        public double Step { get; }

        public WeatherColorEntry(string id, string sub, string displayName, Color defaultColor, double defaultMultipler, double maximum) {
            Id = id;
            Sub = sub;
            DefaultMultipler = defaultMultipler;
            Maximum = maximum;
            Step = maximum / 10d;
            DefaultColor = defaultColor;
            DisplayName = displayName;
        }

        private Color _color;

        public Color Color {
            get { return _color; }
            set {
                if (Equals(value, _color)) return;
                _color = value;
                OnPropertyChanged();
            }
        }

        private double _multipler;

        public double Multipler {
            get { return _multipler; }
            set {
                if (Equals(value, _multipler)) return;
                _multipler = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MultiplerRounded));
            }
        }

        public double MultiplerRounded {
            get { return _multipler; }
            set { Multipler = value.Round(0.01); }
        }
    }
}