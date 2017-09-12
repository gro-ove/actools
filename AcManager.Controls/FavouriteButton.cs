using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AcTools.Utils;

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

    public class RatingBar : Control {
        static RatingBar() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RatingBar), new FrameworkPropertyMetadata(typeof(RatingBar)));
        }

        public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(nameof(ActiveBrush), typeof(Brush),
                typeof(RatingBar));

        public Brush ActiveBrush {
            get => (Brush)GetValue(ActiveBrushProperty);
            set => SetValue(ActiveBrushProperty, value);
        }

        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(PathData),
                typeof(RatingBar));

        public PathData Data {
            get => (PathData)GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(nameof(Rating), typeof(double),
                typeof(RatingBar), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public double Rating {
            get => GetValue(RatingProperty) as double? ?? default(double);
            set => SetValue(RatingProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
                typeof(RatingBar));

        public bool IsReadOnly {
            get => GetValue(IsReadOnlyProperty) as bool? ?? default(bool);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private void Update(MouseEventArgs e) {
            if (IsReadOnly) return;
            Rating = (5d * e.GetPosition(this).X / ActualWidth).Round(0.5);
            Focus();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);
            Update(e);
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed) {
                Update(e);
            }
        }
    }
}