using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace AcManager.Controls {
    public class FavouriteButton : ToggleButton {
        static FavouriteButton() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FavouriteButton), new FrameworkPropertyMetadata(typeof(FavouriteButton)));
        }

        public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(nameof(ActiveBrush), typeof(Brush),
                typeof(FavouriteButton));

        public Brush ActiveBrush {
            get { return (Brush)GetValue(ActiveBrushProperty); }
            set { SetValue(ActiveBrushProperty, value); }
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(PathData),
                typeof(FavouriteButton));

        public PathData Data {
            get { return (PathData)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }
    }
}