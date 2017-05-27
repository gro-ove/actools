using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Attached;
using FlowDirection = System.Windows.FlowDirection;
using Panel = System.Windows.Controls.Panel;

namespace AcManager.Controls {
    public class PropertiesGrid : Panel {
        static PropertiesGrid() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertiesGrid), new FrameworkPropertyMetadata(typeof(PropertiesGrid)));
        }

        public static readonly DependencyProperty FirstColumnProperty = DependencyProperty.Register(nameof(FirstColumn), typeof(int), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int FirstColumn {
            get { return (int)GetValue(FirstColumnProperty); }
            set { SetValue(FirstColumnProperty, value); }
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public int Columns {
            get { return (int)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int),
                typeof(PropertiesGrid));

        public int Rows {
            get { return (int)GetValue(RowsProperty); }
            set { SetValue(RowsProperty, value); }
        }

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double),
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double HorizontalSpacing {
            get { return (double)GetValue(HorizontalSpacingProperty); }
            set { SetValue(HorizontalSpacingProperty, value); }
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(double),
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double VerticalSpacing {
            get { return (double)GetValue(VerticalSpacingProperty); }
            set { SetValue(VerticalSpacingProperty, value); }
        }

        public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

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
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(OnLabelChanged));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var parent = (d as FrameworkElement)?.Parent as PropertiesGrid;
            if (parent != null) {
                parent.InvalidateArrange();
                parent.InvalidateVisual();
            }
        }

        private readonly ToolTip _toolTip;

        public PropertiesGrid() {
            Background = new SolidColorBrush(Colors.Transparent);
            ToolTip = _toolTip = new ToolTip { Visibility = Visibility.Hidden };
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            PreviewMouseMove += OnMouseMove;
        }

        private int GetChildId(MouseEventArgs e, out double relx) {
            var loc = e.GetPosition(this);

            var row = (loc.Y / _yStep).FloorToInt();
            var column = (loc.X / _xStep).FloorToInt();

            var id = row * _columns + column;
            relx = loc.X - column * _xStep;

            return id;
        }

        private int GetChildId(MouseEventArgs e) {
            double relx;
            var id = GetChildId(e, out relx);
            return relx > _labelWidth ? -1 : id;
        }

        private int _id = -1;
        private FrameworkElement _toolTipDonor;
        private object _toolTipDonored;

        private void OnMouseMove(object sender, MouseEventArgs e) {
            var id = GetChildId(e);
            if (id == _id) return;

            _id = id;

            var child = InternalChildren.OfType<FrameworkElement>().ElementAtOrDefault(id);
            var childToolTip = child?.ToolTip;

            /* if (ReferenceEquals(_toolTipDonor, child)) return;

            if (_toolTipDonor != null) {
                ToolTip = null;
                _toolTipDonor.ToolTip = _toolTipDonored;
                _toolTipDonor = null;
                _toolTipDonored = null;
            }

            if (childToolTip != null) {
                _toolTipDonored = childToolTip;
                _toolTipDonor = child;
                _toolTipDonor.ToolTip = null;
                ToolTip = _toolTipDonored;
            }*/

            if (childToolTip == null) {
                _toolTip.Visibility = Visibility.Hidden;
                // ToolTip = null;
            } else {
                _toolTip.Visibility = Visibility.Visible;
                // ToolTip = _toolTip;

                var toolTip = childToolTip as ToolTip;
                if (toolTip == null) {
                    _toolTip.Content = childToolTip;
                    _toolTip.ContentStringFormat = null;
                    _toolTip.ContentTemplate = null;
                    _toolTip.ContentTemplateSelector = null;
                } else {
                    _toolTip.Content = toolTip.Content;
                    _toolTip.ContentStringFormat = toolTip.ContentStringFormat;
                    _toolTip.ContentTemplate = toolTip.ContentTemplate;
                    _toolTip.ContentTemplateSelector = toolTip.ContentTemplateSelector;
                }
            }
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            double relx;
            var child = InternalChildren.OfType<FrameworkElement>().ElementAtOrDefault(GetChildId(e, out relx));
            var menu = child?.ContextMenu;
            if (menu != null) {
                if (relx < _labelWidth) {
                    menu.IsOpen = true;
                    e.Handled = true;
                } else if (ContextMenuAdvancement.GetPropagateToChildren(child)) {
                    ContextMenuAdvancement.PropagateToChildren(child);
                }
            }
        }

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

                if (_labels[i] != null) {
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
        }

        private void UpdateLabels() {
            if (_labels?.Length != _nonCollapsedCount) {
                _labels = new string[_nonCollapsedCount];
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
            _labelTextWidth = _labelWidth - _labelPadding.Left - _labelPadding.Right;
        }

        private void UpdateComputedValues() {
            _columns = Columns;
            _rows = Rows;
            _labelWidth = LabelWidth;

            if (FirstColumn >= _columns) {
                FirstColumn = 0;
            }

            var nonCollapsedCount = 0;

            if (_rows == 0 || _columns == 0) {
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
            } else {
                for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                    var child = InternalChildren[i];
                    if (child.Visibility != Visibility.Collapsed) {
                        nonCollapsedCount++;
                    }
                }
            }

            _nonCollapsedCount = nonCollapsedCount;

            _horizontalSpacing = HorizontalSpacing;
            _verticalSpacing = VerticalSpacing;

            _totalSpacingWidth = _columns == 0 ? 0 : _horizontalSpacing * (_columns - 1) + _labelWidth * _columns;
            _totalSpacingHeight = _rows == 0 ? 0 : _verticalSpacing * (_rows - 1);
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();

            if (_columns == 0 || _rows == 0) return default(Size);
            var childConstraint = new Size(Math.Max(constraint.Width - _totalSpacingWidth, 0d) / _columns, Math.Max((constraint.Height - _totalSpacingHeight) / _rows, 0d));
            var maxChildDesiredWidth = 0d;
            var maxChildDesiredHeight = 0d;

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

            return new Size(maxChildDesiredWidth * _columns + _totalSpacingWidth, maxChildDesiredHeight * _rows + _totalSpacingHeight);
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            if (_columns == 0 || _rows == 0) return default(Size);
            UpdateLabels();

            var totalSpacingX = _columns == 0 ? 0 : _horizontalSpacing * (_columns - 1);

            _xStep = (arrangeSize.Width - totalSpacingX) / _columns;
            _yStep = Math.Max(arrangeSize.Height - _totalSpacingHeight, 0d) / _rows;
            _labelTextOffset = (_yStep + _labelPadding.Top - _labelPadding.Bottom) / 2;

            var xBound = arrangeSize.Width - 1.0;
            var childBounds = new Rect(_labelWidth, 0, Math.Max(_xStep - _labelWidth, 0d), _yStep);

            _xStep += _horizontalSpacing;
            _yStep += _verticalSpacing;

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
        private int _nonCollapsedCount;
        private double _horizontalSpacing;
        private double _verticalSpacing;
        private double _totalSpacingWidth;
        private double _totalSpacingHeight;
    }
}