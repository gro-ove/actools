using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AcManager.Controls {
    public class PropertiesGrid : Panel {
        static PropertiesGrid() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertiesGrid), new FrameworkPropertyMetadata(typeof(PropertiesGrid)));
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Columns {
            get { return (int)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double),
                typeof(PropertiesGrid));

        public double Spacing {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double LabelWidth {
            get { return (double)GetValue(LabelWidthProperty); }
            set { SetValue(LabelWidthProperty, value); }
        }

        public static readonly DependencyProperty LabelFontFamilyProperty = DependencyProperty.Register(nameof(LabelFontFamily), typeof(FontFamily),
                typeof(PropertiesGrid));

        public FontFamily LabelFontFamily {
            get { return (FontFamily)GetValue(LabelFontFamilyProperty); }
            set { SetValue(LabelFontFamilyProperty, value); }
        }

        public static readonly DependencyProperty LabelFontWeightProperty = DependencyProperty.Register(nameof(LabelFontWeight), typeof(FontWeight),
                typeof(PropertiesGrid));

        public FontWeight LabelFontWeight {
            get { return (FontWeight)GetValue(LabelFontWeightProperty); }
            set { SetValue(LabelFontWeightProperty, value); }
        }

        public static readonly DependencyProperty LabelPaddingProperty = DependencyProperty.Register(nameof(LabelPadding), typeof(Thickness),
                typeof(PropertiesGrid));

        public Thickness LabelPadding {
            get { return (Thickness)GetValue(LabelPaddingProperty); }
            set { SetValue(LabelPaddingProperty, value); }
        }

        public static string GetLabel(DependencyObject obj) {
            return (string)obj.GetValue(LabelProperty);
        }

        public static void SetLabel(DependencyObject obj, string value) {
            obj.SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached("Label", typeof(string),
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        private Typeface _labelTypeface;
        private Thickness _labelPadding;
        private double _labelWidth;
        private double _labelTextWidth;
        private double _labelFontSize;
        private TextFormattingMode _formattingMode;
        private Brush _labelForeground;
        private string[] _labels;
        private double _xStep, _yStep;
        private double _labelTextOffset;

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            
            var y = 0.0;
            for (int i = 0, c = 0; i < _labels.Length; i++, c++) {
                if (c == _columns) {
                    c = 0;
                    y += _yStep;
                }

                var text = new FormattedText(_labels[i], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                        _labelTypeface, _labelFontSize, _labelForeground, new NumberSubstitution(), _formattingMode) {
                    Trimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Left,
                    MaxLineCount = 1,
                    MaxTextWidth = _labelTextWidth
                };

                dc.DrawText(text, new Point(_xStep * c + _labelPadding.Left, y + _labelTextOffset - text.Height / 2));
            }
        }

        private void UpdateComputedValues() {
            _columns = Columns;
            _rows = 0;
            _spacing = Spacing;

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

            _rows = (nonCollapsedCount + (_columns - 1)) / _columns;

            if (_labels?.Length != nonCollapsedCount) {
                _labels = new string[nonCollapsedCount];
            }

            for (int i = 0, j = 0, count = InternalChildren.Count; i < count; ++i) {
                var child = InternalChildren[i];
                if (child.Visibility != Visibility.Collapsed) {
                    var value = child.GetValue(LabelProperty)?.ToString();
                    _labels[j++] = string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }

            // Label-related values
            _labelTypeface = new Typeface(LabelFontFamily, FontStyles.Normal, LabelFontWeight, FontStretches.Normal);
            _labelForeground = (Brush)GetValue(TextBlock.ForegroundProperty);
            _labelFontSize = (double)GetValue(TextBlock.FontSizeProperty);
            _formattingMode = (TextFormattingMode)GetValue(TextOptions.TextFormattingModeProperty);
            _labelPadding = LabelPadding;
            _labelWidth = LabelWidth;
            _labelTextWidth = _labelWidth - _labelPadding.Left - _labelPadding.Right;
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();

            if (_columns == 0 || _rows == 0) return default(Size);
            var totalSpacingX = _spacing * (_columns - 1);
            var totalSpacingY = _spacing * (_rows - 1);

            var childConstraint = new Size(constraint.Width / _columns - totalSpacingX - _labelWidth, constraint.Height / _rows - totalSpacingY);
            var maxChildDesiredWidth = 0.0;
            var maxChildDesiredHeight = 0.0;
            
            for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                var child = InternalChildren[i];
                
                child.Measure(childConstraint);
                var childDesiredSize = child.DesiredSize;

                if (maxChildDesiredWidth < childDesiredSize.Width) {
                    maxChildDesiredWidth = childDesiredSize.Width;
                }

                if (maxChildDesiredHeight < childDesiredSize.Height) {
                    maxChildDesiredHeight = childDesiredSize.Height;
                }
            }

            return new Size(maxChildDesiredWidth * _columns + totalSpacingX, maxChildDesiredHeight * _rows + totalSpacingY);
        }
        
        protected override Size ArrangeOverride(Size arrangeSize) {
            if (_columns == 0 || _rows == 0) return default(Size);
            var totalSpacingX = _spacing * (_columns - 1);
            var totalSpacingY = _spacing * (_rows - 1);

            _xStep = (arrangeSize.Width - totalSpacingX) / _columns;
            _yStep = (arrangeSize.Height - totalSpacingY) / _rows;
            _labelTextOffset = (_yStep + _labelPadding.Top - _labelPadding.Bottom) / 2;

            var xBound = arrangeSize.Width - 1.0;
            var childBounds = new Rect(_labelWidth, 0, _xStep - _labelWidth, _yStep);

            _xStep += _spacing;
            _yStep += _spacing;
            
            foreach (UIElement child in InternalChildren) {
                var delta = childBounds.Height - child.DesiredSize.Height;
                if (delta > 0) {
                    childBounds.Y += delta / 2d;
                    child.Arrange(childBounds);
                    childBounds.Y -= delta / 2d;
                } else {
                    child.Arrange(childBounds);
                }
                
                if (child.Visibility != Visibility.Collapsed) {
                    childBounds.X += _xStep;
                    if (childBounds.X >= xBound) {
                        childBounds.Y += _yStep;
                        childBounds.X = _labelWidth;
                    }
                }
            }

            return arrangeSize;
        }

        private int _rows;
        private int _columns;
        private double _spacing;
    }
}