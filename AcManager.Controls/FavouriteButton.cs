using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AcManager.Controls {
    public class FavouriteButton : ToggleButton {
        static FavouriteButton() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FavouriteButton), new FrameworkPropertyMetadata(typeof(FavouriteButton)));
        }

        public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(nameof(ActiveBrush), typeof(Brush),
                typeof(FavouriteButton));

        public Brush ActiveBrush {
            get => (Brush)GetValue(ActiveBrushProperty);
            set => SetValue(ActiveBrushProperty, value);
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(PathData),
                typeof(FavouriteButton));

        public PathData Data {
            get => (PathData)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
    }
}