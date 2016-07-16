using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class UniformGridWithOrientation : UniformGrid {
        #region Orientation (Dependency Property)  
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(Orientation),
                typeof(UniformGridWithOrientation), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure),
                IsValidOrientation);

        internal static bool IsValidOrientation(object o) {
            var n = (Orientation)o;
            return n == Orientation.Horizontal || n == Orientation.Vertical;
        }

        public Orientation Orientation {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }
        #endregion

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();
            var available = new Size(constraint.Width / _columns, constraint.Height / _rows);
            var width = 0d;
            var height = 0d;
            var num3 = 0;
            var count = InternalChildren.Count;
            while (num3 < count) {
                var element = InternalChildren[num3];
                element.Measure(available);
                var desiredSize = element.DesiredSize;
                if (width < desiredSize.Width) {
                    width = desiredSize.Width;
                }
                if (height < desiredSize.Height) {
                    height = desiredSize.Height;
                }
                num3++;
            }
            return new Size(width * _columns, height * _rows);
        }

        private int _columns;
        private int _rows;

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
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            var final = new Rect(0.0, 0.0, arrangeSize.Width / _columns, arrangeSize.Height / _rows);
            var height = final.Height;
            var x = arrangeSize.Height - 1.0;
            final.X += final.Width * FirstColumn;
            foreach (UIElement element in InternalChildren) {
                element.Arrange(final);
                if (element.Visibility == Visibility.Collapsed) continue;

                final.Y += height;
                if (final.Y < x) continue;

                final.X += final.Width;
                final.Y = 0.0;
            }
            return arrangeSize;
        }
    }
}