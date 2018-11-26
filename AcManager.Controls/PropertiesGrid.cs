using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcTools.Utils;
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
            get => GetValue(FirstColumnProperty) as int? ?? 0;
            set => SetValue(FirstColumnProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public int Columns {
            get => GetValue(ColumnsProperty) as int? ?? 1;
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int),
                typeof(PropertiesGrid));

        public int Rows {
            get => GetValue(RowsProperty) as int? ?? 1;
            set => SetValue(RowsProperty, value);
        }

        public static readonly DependencyProperty WithoutMarginForEmptyLabelsProperty = DependencyProperty.Register(nameof(WithoutMarginForEmptyLabels),
                typeof(bool), typeof(PropertiesGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool WithoutMarginForEmptyLabels {
            get => GetValue(WithoutMarginForEmptyLabelsProperty) as bool? ?? false;
            set => SetValue(WithoutMarginForEmptyLabelsProperty, value);
        }

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double),
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double HorizontalSpacing {
            get => GetValue(HorizontalSpacingProperty) as double? ?? 0d;
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(nameof(VerticalSpacing), typeof(double),
                typeof(PropertiesGrid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double VerticalSpacing {
            get => GetValue(VerticalSpacingProperty) as double? ?? 0d;
            set => SetValue(VerticalSpacingProperty, value);
        }

        public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(PropertiesGrid),
                new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double LabelWidth {
            get => GetValue(LabelWidthProperty) as double? ?? 0d;
            set => SetValue(LabelWidthProperty, value);
        }

        public static readonly DependencyProperty LabelFontFamilyProperty = DependencyProperty.Register(nameof(LabelFontFamily), typeof(FontFamily),
                typeof(PropertiesGrid));

        public FontFamily LabelFontFamily {
            get => (FontFamily)GetValue(LabelFontFamilyProperty);
            set => SetValue(LabelFontFamilyProperty, value);
        }

        public static readonly DependencyProperty LabelFontWeightProperty = DependencyProperty.Register(nameof(LabelFontWeight), typeof(FontWeight),
                typeof(PropertiesGrid));

        public FontWeight LabelFontWeight {
            get => GetValue(LabelFontWeightProperty) as FontWeight? ?? default;
            set => SetValue(LabelFontWeightProperty, value);
        }

        public static readonly DependencyProperty LabelPaddingProperty = DependencyProperty.Register(nameof(LabelPadding), typeof(Thickness),
                typeof(PropertiesGrid));

        public Thickness LabelPadding {
            get => GetValue(LabelPaddingProperty) as Thickness? ?? default;
            set => SetValue(LabelPaddingProperty, value);
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
        // private FrameworkElement _toolTipDonor;
        // private object _toolTipDonored;

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
        private UIElement[] _labelElements;
        private double _xStep, _yStep;
        private double _labelTextOffset;

        /*protected override void OnChildDesiredSizeChanged(UIElement child) {
            base.OnChildDesiredSizeChanged(child);
            InvalidateMeasure();
            InvalidateVisual();
        }*/

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            var y = 0.0;
            for (int i = 0, c = 0; i < _labels.Length; i++, c++) {
                if (c == _columns) {
                    c = 0;
                    y += _yStep;
                }

                if (_labels[i] != null && _labelElements[i].Visibility != Visibility.Collapsed) {
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
                _labelElements = new UIElement[_nonCollapsedCount];
            }

            var children = InternalChildren;
            for (int i = 0, j = 0, count = children.Count; i < count; ++i) {
                var child = children[i];
                if (child.Visibility != Visibility.Collapsed) {
                    var value = child.GetValue(LabelProperty)?.ToString();
                    _labelElements[j] = child;
                    _labels[j++] = string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }

            // Label-related values
            _labelTypeface = new Typeface(LabelFontFamily, FontStyles.Normal, LabelFontWeight, FontStretches.Normal);
            _labelForeground = (Brush)GetValue(TextBlock.ForegroundProperty);
            _labelFontSize = GetValue(TextBlock.FontSizeProperty) as double? ?? 18d;
            _formattingMode = GetValue(TextOptions.TextFormattingModeProperty) as TextFormattingMode? ?? TextFormattingMode.Display;
            _labelPadding = LabelPadding;
            _labelTextWidth = _labelWidth - _labelPadding.Left - _labelPadding.Right;
            _withoutMarginForEmptyLabels = WithoutMarginForEmptyLabels;
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

            _totalSpacingWidthWithoutLabelMargin = _columns == 0 ? 0 : _horizontalSpacing * (_columns - 1);
            _totalSpacingWidth = _totalSpacingWidthWithoutLabelMargin + _labelWidth * _columns;
            _totalSpacingHeight = _rows == 0 ? 0 : _verticalSpacing * (_rows - 1);
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateComputedValues();

            if (_columns == 0 || _rows == 0) return default;
            var childConstraint =
                    new Size(Math.Max(constraint.Width - _totalSpacingWidth, 0d) / _columns,
                            Math.Max((constraint.Height - _totalSpacingHeight) / _rows, 0d));
            var childConstraintNoLabel = _withoutMarginForEmptyLabels ?
                    new Size(Math.Max(constraint.Width - _totalSpacingWidthWithoutLabelMargin, 0d) / _columns,
                            Math.Max((constraint.Height - _totalSpacingHeight) / _rows, 0d)) : childConstraint;

            var maxChildDesiredWidth = 0d;
            var maxChildDesiredHeight = 0d;

            for (int i = 0, count = InternalChildren.Count; i < count; ++i) {
                var child = InternalChildren[i];
                var noLabel = _withoutMarginForEmptyLabels && _labels[i] == null;
                child.Measure(noLabel ? childConstraintNoLabel : childConstraint);
                var childDesiredSize = child.DesiredSize;
                var width = noLabel ? childDesiredSize.Width - _labelWidth : childDesiredSize.Width;

                if (maxChildDesiredWidth < width) {
                    maxChildDesiredWidth = width;
                }

                if (maxChildDesiredHeight < childDesiredSize.Height) {
                    maxChildDesiredHeight = childDesiredSize.Height;
                }
            }

            return new Size(maxChildDesiredWidth * _columns + _totalSpacingWidth, maxChildDesiredHeight * _rows + _totalSpacingHeight);
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            if (_columns == 0 || _rows == 0) return default;
            UpdateLabels();

            var totalSpacingX = _columns == 0 ? 0 : _horizontalSpacing * (_columns - 1);

            _xStep = (arrangeSize.Width - totalSpacingX) / _columns;
            _yStep = Math.Max(arrangeSize.Height - _totalSpacingHeight, 0d) / _rows;
            _labelTextOffset = (_yStep + _labelPadding.Top - _labelPadding.Bottom) / 2;

            var xBound = arrangeSize.Width - 1.0;
            var childBounds =
                    new Rect(_labelWidth, 0, Math.Max(_xStep - _labelWidth, 0d), _yStep);
            var childBoundsNoLabel = _withoutMarginForEmptyLabels ?
                    new Rect(_labelPadding.Left, 0, Math.Max(_xStep - _labelPadding.Left, 0d), _yStep) : childBounds;

            _xStep += _horizontalSpacing;
            _yStep += _verticalSpacing;

            for (var i = 0; i < InternalChildren.Count; i++) {
                var child = InternalChildren[i];
                var noLabel = _withoutMarginForEmptyLabels && _labels[i] == null;

                var delta = childBounds.Height - child.DesiredSize.Height;
                var bounds = noLabel ? childBoundsNoLabel : childBounds;
                if (noLabel) {
                    bounds.X = childBounds.X - _labelWidth + _labelPadding.Left;
                    bounds.Y = childBounds.Y;
                }

                if (delta > 0) {
                    bounds.Y += delta / 2d;
                    child.Arrange(bounds);
                } else {
                    child.Arrange(bounds);
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
        private bool _withoutMarginForEmptyLabels;
        private int _nonCollapsedCount;
        private double _horizontalSpacing;
        private double _verticalSpacing;
        private double _totalSpacingWidth;
        private double _totalSpacingWidthWithoutLabelMargin;
        private double _totalSpacingHeight;
    }
}