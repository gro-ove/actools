using System;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class SpacingUniformGrid : Panel {
        public static readonly DependencyProperty FirstColumnProperty = DependencyProperty.Register(nameof(FirstColumn), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int FirstColumn {
            get => (int)GetValue(FirstColumnProperty);
            set => SetValue(FirstColumnProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Columns {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Rows {
            get => (int)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double),
                typeof(SpacingUniformGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double HorizontalSpacing {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(double),
                typeof(SpacingUniformGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double VerticalSpacing {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public static readonly DependencyProperty StackModeProperty = DependencyProperty.Register(nameof(StackMode), typeof(bool),
                typeof(SpacingUniformGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool StackMode {
            get => (bool)GetValue(StackModeProperty);
            set => SetValue(StackModeProperty, value);
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();

            if (_stackMode) {
                var childConstraint = new Size(
                        Math.Max(constraint.Width - _totalSpacingWidth, 0) / _columns,
                        double.PositiveInfinity);

                var maxChildDesiredWidth = 0d;
                var summaryChildrenHeight = 0d;
                var rowHeight = 0d;
                var itemsInRow = 0;

                //  Measure each child, keeping track of maximum desired width and height.
                for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                    var child = InternalChildren[i];

                    // Measure the child.
                    child.Measure(childConstraint);
                    var childDesiredSize = child.DesiredSize;

                    if (maxChildDesiredWidth < childDesiredSize.Width) {
                        maxChildDesiredWidth = childDesiredSize.Width;
                    }

                    if (childDesiredSize.Height > rowHeight) {
                        rowHeight = childDesiredSize.Height;
                    }

                    if (++itemsInRow == _columns) {
                        itemsInRow = 0;
                        summaryChildrenHeight += rowHeight;
                    }
                }

                return new Size(maxChildDesiredWidth * _columns + _totalSpacingWidth, summaryChildrenHeight + _totalSpacingHeight);
            } else {
                var childConstraint = new Size(
                        Math.Max(constraint.Width - _totalSpacingWidth, 0) / _columns,
                        Math.Max(constraint.Height - _totalSpacingHeight, 0) / _rows);

                var maxChildDesiredWidth = 0d;
                var maxChildDesiredHeight = 0d;

                //  Measure each child, keeping track of maximum desired width and height.
                for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                    var child = InternalChildren[i];

                    // Measure the child.
                    child.Measure(childConstraint);
                    var childDesiredSize = child.DesiredSize;

                    if (maxChildDesiredWidth < childDesiredSize.Width) {
                        maxChildDesiredWidth = childDesiredSize.Width;
                    }

                    if (maxChildDesiredHeight < childDesiredSize.Height) {
                        maxChildDesiredHeight = childDesiredSize.Height;
                    }
                }

                return new Size(maxChildDesiredWidth * _columns + _totalSpacingWidth, maxChildDesiredHeight * _rows + _totalSpacingHeight);
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            var children = InternalChildren;

            if (_stackMode) {
                var childBounds = new Rect(0, 0, (arrangeSize.Width - _totalSpacingWidth) / _columns, 0);
                var xStep = childBounds.Width + _horizontalSpacing;
                var maxHeight = 0d;
                var xBound = arrangeSize.Width - 1.0;

                childBounds.X += xStep * FirstColumn;

                // Arrange and Position each child to the same cell size
                for (var i = 0; i < children.Count; i++) {
                    var child = children[i];
                    childBounds.Height = child.DesiredSize.Height;
                    child.Arrange(childBounds);

                    // only advance to the next grid cell if the child was not collapsed
                    if (child.Visibility != Visibility.Collapsed) {
                        childBounds.X += xStep;

                        if (childBounds.Height > maxHeight) {
                            maxHeight = childBounds.Height;
                        }

                        if (childBounds.X >= xBound) {
                            childBounds.Y += maxHeight + _verticalSpacing;
                            childBounds.X = 0;
                        }
                    }
                }
            } else {
                var childBounds = new Rect(0, 0, (arrangeSize.Width - _totalSpacingWidth) / _columns, (arrangeSize.Height - _totalSpacingHeight) / _rows);
                var xStep = childBounds.Width + _horizontalSpacing;
                var yStep = childBounds.Height + _verticalSpacing;
                var xBound = arrangeSize.Width - 1.0;

                childBounds.X += xStep * FirstColumn;

                // Arrange and Position each child to the same cell size
                for (var i = 0; i < children.Count; i++) {
                    var child = children[i];
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
            }

            return arrangeSize;
        }

        private void UpdateComputedValues() {
            _stackMode = StackMode;
            _columns = Columns;
            _rows = Rows;

            if (FirstColumn >= _columns) {
                FirstColumn = 0;
            }

            if (_rows == 0 || _columns == 0) {
                var nonCollapsedCount = 0;

                for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                    var child = InternalChildren[i];
                    if (child.Visibility != Visibility.Collapsed) {
                        nonCollapsedCount++;
                    }
                }

                if (nonCollapsedCount == 0) {
                    nonCollapsedCount = 1;
                }

                if (_rows == 0) {
                    if (_columns > 0) {
                        _rows = (nonCollapsedCount + FirstColumn + (_columns - 1)) / _columns;
                    } else {
                        _rows = (int)Math.Sqrt(nonCollapsedCount);
                        if (_rows * _rows < nonCollapsedCount) {
                            _rows++;
                        }
                        _columns = _rows;
                    }
                } else if (_columns == 0) {
                    _columns = (nonCollapsedCount + (_rows - 1)) / _rows;
                }
            }

            _horizontalSpacing = HorizontalSpacing;
            _verticalSpacing = VerticalSpacing;

            _totalSpacingWidth = _columns == 0 ? 0 : _horizontalSpacing * (_columns - 1);
            _totalSpacingHeight = _rows == 0 ? 0 : _verticalSpacing * (_rows - 1);
        }

        private bool _stackMode;
        private int _rows;
        private int _columns;
        private double _horizontalSpacing;
        private double _verticalSpacing;
        private double _totalSpacingWidth;
        private double _totalSpacingHeight;
    }
}