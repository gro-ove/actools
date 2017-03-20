using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class SpacingStackPanel : Panel {
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation),
                typeof(SpacingStackPanel), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public Orientation Orientation {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double),
                typeof(SpacingStackPanel), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double Spacing {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        protected override Size MeasureOverride(Size constraint) {
            _orientation = Orientation;
            _spacing = Spacing;

            var count = InternalChildren.Count;
            if (count == 0) return Size.Empty;

            if (_orientation == Orientation.Vertical) {
                var childConstraint = new Size(constraint.Width, double.PositiveInfinity);
                var maxWidth = 0d;
                var summaryHeight = 0d;
                var first = true;
                for (var i = 0; i < count; ++i) {
                    var child = InternalChildren[i];
                    child.Measure(childConstraint);

                    if (child.Visibility != Visibility.Collapsed) {
                        var desiredSize = child.DesiredSize;
                        if (desiredSize.Width > maxWidth) {
                            maxWidth = desiredSize.Width;
                        }

                        summaryHeight += desiredSize.Height + (first ? 0 : _spacing);
                        first = false;
                    }
                }

                return new Size(maxWidth, summaryHeight);
            } else {
                var childConstraint = new Size(double.PositiveInfinity, constraint.Height);
                var maxHeight = 0d;
                var summaryWidth = 0d;
                var first = true;
                for (var i = 0; i < count; ++i) {
                    var child = InternalChildren[i];
                    child.Measure(childConstraint);

                    if (child.Visibility != Visibility.Collapsed) {
                        var desiredSize = child.DesiredSize;
                        if (desiredSize.Height > maxHeight) {
                            maxHeight = desiredSize.Height;
                        }

                        summaryWidth += desiredSize.Width + (first ? 0 : _spacing);
                        first = false;
                    }
                }

                return new Size(summaryWidth, maxHeight);
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            var count = InternalChildren.Count;
            if (count == 0) return arrangeSize;

            if (_orientation == Orientation.Vertical) {
                var childBounds = new Rect(0, 0, arrangeSize.Width, 0);
                for (var i = 0; i < count; ++i) {
                    var child = InternalChildren[i];
                    var size = child.DesiredSize.Height;
                    childBounds.Height = size;
                    child.Arrange(childBounds);
                    if (child.Visibility != Visibility.Collapsed) {
                        childBounds.Y += size + _spacing;
                    }
                }
            } else {
                var childBounds = new Rect(0, 0, 0, arrangeSize.Height);
                for (var i = 0; i < count; ++i) {
                    var child = InternalChildren[i];
                    var size = child.DesiredSize.Width;
                    childBounds.Width = size;
                    child.Arrange(childBounds);
                    if (child.Visibility != Visibility.Collapsed) {
                        childBounds.X += size + _spacing;
                    }
                }
            }

            return arrangeSize;
        }

        private Orientation _orientation;
        private double _spacing;
    }
}