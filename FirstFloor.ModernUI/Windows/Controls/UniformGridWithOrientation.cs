using System;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class UniformGridWithOrientation : SpacingUniformGrid {
        #region Orientation (Dependency Property)
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(Orientation),
                typeof(UniformGridWithOrientation), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure),
                IsValidOrientation);

        internal static bool IsValidOrientation(object o) {
            var n = (Orientation)o;
            return n == Orientation.Horizontal || n == Orientation.Vertical;
        }

        public Orientation Orientation {
            get => GetValue(OrientationProperty) as Orientation? ?? default;
            set => SetValue(OrientationProperty, value);
        }
        #endregion

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();
            if (_columns == 0 || _rows == 0) return new Size();

            var available = new Size(
                    Math.Max((constraint.Width - _totalSpacingWidth) / _columns, 0),
                    Math.Max((constraint.Height - _totalSpacingHeight) / _rows, 0));

            var width = 0d;
            var height = 0d;
            var count = InternalChildren.Count;

            for (var n = 0; n < count; n++) {
                var element = InternalChildren[n];
                element.Measure(available);

                var desiredSize = element.DesiredSize;

                if (width < desiredSize.Width) {
                    width = desiredSize.Width;
                }

                if (height < desiredSize.Height) {
                    height = desiredSize.Height;
                }

                n++;
            }

            return new Size(width * _columns + _totalSpacingWidth, height * _rows + _totalSpacingHeight);
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            if (_columns == 0 || _rows == 0) return new Size();

            var childBounds = new Rect(0, 0,
                    Math.Max((arrangeSize.Width - _totalSpacingWidth) / _columns, 0),
                    Math.Max((arrangeSize.Height - _totalSpacingHeight) / _rows, 0));

            var xStep = childBounds.Width + _horizontalSpacing;
            var yStep = childBounds.Height + _verticalSpacing;

            switch (_orientation) {
                case Orientation.Horizontal:
                    var xBound = arrangeSize.Width - 1.0;
                    childBounds.X += xStep * FirstColumn;

                    // Arrange and Position each child to the same cell size
                    foreach (UIElement child in InternalChildren) {
                        child.Arrange(childBounds);

                        // only advance to the next grid cell if the child was not collapsed
                        if (child.Visibility != Visibility.Collapsed) {
                            childBounds.X += xStep;
                            if (childBounds.X >= xBound) {
                                childBounds.Y += yStep;
                                childBounds.X = 0;
                            }
                        }
                    }
                    break;

                case Orientation.Vertical:
                    var yBound = arrangeSize.Height - 1.0;
                    childBounds.Y += yStep * FirstColumn;

                    // Arrange and Position each child to the same cell size
                    foreach (UIElement child in InternalChildren) {
                        child.Arrange(childBounds);

                        // only advance to the next grid cell if the child was not collapsed
                        if (child.Visibility != Visibility.Collapsed) {
                            childBounds.Y += yStep;
                            if (childBounds.Y >= yBound) {
                                childBounds.X += xStep;
                                childBounds.Y = 0;
                            }
                        }
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return arrangeSize;
        }

        private void UpdateComputedValues() {
            _columns = Columns;
            _rows = Rows;

            if (FirstColumn >= _columns) {
                FirstColumn = 0;
            }

            if (FirstColumn > 0) {
                throw new NotImplementedException("There is no support for setting the FirstColumn (nor the FirstRow)");
            }

            if (_rows == 0 || _columns == 0) {
                var n = 0;
                var m = 0;
                var c = InternalChildren.Count;
                while (m < c) {
                    var element = InternalChildren[m];
                    if (element.Visibility != Visibility.Collapsed) {
                        n++;
                    }
                    m++;
                }
                if (n == 0) {
                    n = 1;
                }
                if (_rows == 0) {
                    if (_columns > 0) {
                        _rows = (n + FirstColumn + (_columns - 1)) / _columns;
                    } else {
                        _rows = (int)Math.Sqrt(n);
                        if (_rows * _rows < n) {
                            _rows++;
                        }
                        _columns = _rows;
                    }
                } else if (_columns == 0) {
                    _columns = (n + (_rows - 1)) / _rows;
                }
            }

            _horizontalSpacing = HorizontalSpacing;
            _verticalSpacing = VerticalSpacing;

            _totalSpacingWidth = _columns == 0 ? 0 : HorizontalSpacing * Math.Max(_columns - 1, 0);
            _totalSpacingHeight = _rows == 0 ? 0 : VerticalSpacing * Math.Max(_rows - 1, 0);

            _orientation = Orientation;
        }

        private int _columns;
        private int _rows;
        private double _horizontalSpacing;
        private double _verticalSpacing;
        private double _totalSpacingWidth;
        private double _totalSpacingHeight;
        private Orientation _orientation;
    }
}