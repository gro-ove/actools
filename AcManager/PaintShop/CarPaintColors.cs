using System.ComponentModel;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class CarPaintColors : NotifyPropertyChanged {
        public CarPaintColors([NotNull] CarPaintColor[] colors) {
            Colors = colors;

            ActualColors = new Color[Colors.Length];
            DrawingColors = new System.Drawing.Color[Colors.Length];
            UpdateActualColors();

            foreach (var color in Colors) {
                color.PropertyChanged += OnColorChanged;
            }
        }

        public CarPaintColors() : this(new CarPaintColor[0]) { }

        public CarPaintColors(Color defaultColor) : this(new[] {
            new CarPaintColor("Color", defaultColor, null),
        }) {}

        [NotNull]
        public CarPaintColor[] Colors { get; }

        private void OnColorChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged(nameof(Colors));
            UpdateActualColors();
        }

        private void UpdateActualColors() {
            for (var i = 0; i < Colors.Length; i++) {
                ActualColors[i] = Colors[i].Value;
                DrawingColors[i] = ActualColors[i].ToColor();
            }
        }

        public readonly Color[] ActualColors;
        public readonly System.Drawing.Color[] DrawingColors;
    }
}