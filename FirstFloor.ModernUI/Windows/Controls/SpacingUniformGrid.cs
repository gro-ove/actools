using System;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class SpacingUniformGrid : Panel {
        public static readonly DependencyProperty FirstColumnProperty = DependencyProperty.Register(nameof(FirstColumn), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int FirstColumn {
            get { return (int)GetValue(FirstColumnProperty); }
            set { SetValue(FirstColumnProperty, value); }
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Columns {
            get { return (int)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int), typeof(SpacingUniformGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Rows {
            get { return (int)GetValue(RowsProperty); }
            set { SetValue(RowsProperty, value); }
        }

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(int),
                typeof(SpacingUniformGrid), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int HorizontalSpacing {
            get { return (int)GetValue(HorizontalSpacingProperty); }
            set { SetValue(HorizontalSpacingProperty, value); }
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(int),
                typeof(SpacingUniformGrid), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int VerticalSpacing {
            get { return (int)GetValue(VerticalSpacingProperty); }
            set { SetValue(VerticalSpacingProperty, value); }
        }
        
        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();

            var childConstraint = new Size((constraint.Width - _totalSpacingWidth) / _columns, (constraint.Height - _totalSpacingHeight) / _rows);
            var maxChildDesiredWidth = 0.0;
            var maxChildDesiredHeight = 0.0;

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

        protected override Size ArrangeOverride(Size arrangeSize) {
            var childBounds = new Rect(0, 0, (arrangeSize.Width - _totalSpacingWidth) / _columns, (arrangeSize.Height - _totalSpacingHeight) / _rows);
            var xStep = childBounds.Width + _horizontalSpacing;
            var yStep = childBounds.Height + _verticalSpacing;
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

            return arrangeSize;
        }

        private void UpdateComputedValues() {
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

            _totalSpacingWidth = _columns == 0 ? 0 : HorizontalSpacing * (_columns - 1);
            _totalSpacingHeight = _rows == 0 ? 0 : VerticalSpacing * (_rows - 1);
        }

        private int _rows;
        private int _columns;
        private int _horizontalSpacing;
        private int _verticalSpacing;
        private int _totalSpacingWidth;
        private int _totalSpacingHeight;
    }
}