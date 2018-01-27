using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class RoundSlider : Slider {
        static RoundSlider() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RoundSlider), new FrameworkPropertyMetadata(typeof(RoundSlider)));
        }

        public static readonly DependencyProperty IsThumbDraggingProperty = DependencyProperty.Register(nameof(IsThumbDragging), typeof(bool),
                typeof(RoundSlider));

        public bool IsThumbDragging {
            get => GetValue(IsThumbDraggingProperty) as bool? == true;
            set => SetValue(IsThumbDraggingProperty, value);
        }

        public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(nameof(TickBrush), typeof(Brush),
                typeof(RoundSlider));

        public Brush TickBrush {
            get => (Brush)GetValue(TickBrushProperty);
            set => SetValue(TickBrushProperty, value);
        }

        public static readonly DependencyProperty TickThicknessProperty = DependencyProperty.Register(nameof(TickThickness), typeof(double),
                typeof(RoundSlider));

        public double TickThickness {
            get => GetValue(TickThicknessProperty) as double? ?? 0d;
            set => SetValue(TickThicknessProperty, value);
        }

        public static readonly DependencyProperty TickLengthProperty = DependencyProperty.Register(nameof(TickLength), typeof(double),
                typeof(RoundSlider));

        public double TickLength {
            get => GetValue(TickLengthProperty) as double? ?? 0d;
            set => SetValue(TickLengthProperty, value);
        }

        public static readonly DependencyProperty TickOffsetProperty = DependencyProperty.Register(nameof(TickOffset), typeof(double),
                typeof(RoundSlider));

        public double TickOffset {
            get => GetValue(TickOffsetProperty) as double? ?? 0d;
            set => SetValue(TickOffsetProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object),
                typeof(RoundSlider));

        public object Content {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentWidthProperty = DependencyProperty.Register(nameof(ContentWidth), typeof(double),
                typeof(RoundSlider));

        public double ContentWidth {
            get => GetValue(ContentWidthProperty) as double? ?? 0d;
            set => SetValue(ContentWidthProperty, value);
        }

        public static readonly DependencyProperty ContentHeightProperty = DependencyProperty.Register(nameof(ContentHeight), typeof(double),
                typeof(RoundSlider));

        public double ContentHeight {
            get => GetValue(ContentHeightProperty) as double? ?? 0d;
            set => SetValue(ContentHeightProperty, value);
        }

        private Thumb _thumb;
        private FrameworkElement _wrapper;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_thumb != null) {
                _thumb.DragDelta -= OnThumbDragDelta;
                _thumb.DragStarted -= OnThumbDragStarted;
                _thumb.DragCompleted -= OnThumbDragCompleted;
            }

            if (_wrapper != null) {
                _wrapper.MouseWheel -= OnWrapperMouseWheel;
            }

            _thumb = GetTemplateChild(@"PART_Thumb") as Thumb;
            _wrapper = GetTemplateChild(@"PART_Wrapper") as FrameworkElement;

            if (_thumb != null) {
                _thumb.DragDelta += OnThumbDragDelta;
                _thumb.DragStarted += OnThumbDragStarted;
                _thumb.DragCompleted += OnThumbDragCompleted;
            }

            if (_wrapper != null) {
                _wrapper.MouseWheel += OnWrapperMouseWheel;
            }
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            if (TickPlacement != TickPlacement.None) {
                var ticks = (Maximum - Minimum) / TickFrequency;
                var p = new Pen(TickBrush, TickThickness);
                var w = ActualWidth / 2;
                var h = ActualHeight / 2;
                var f = -TickOffset - TickLength / 2;
                var t = -TickOffset + TickLength / 2;
                for (var i = 0; i < ticks; i++) {
                    var a = Math.PI * 2 * i / ticks;
                    var x = Math.Sin(a);
                    var y = Math.Cos(a);
                    dc.DrawLine(p, new Point(w + x * (w + f), h + y * (h + f)),  new Point(w + x * (w + t), h + y * (h + t)));
                }
            }
        }

        private void OnThumbDragStarted(object sender, DragStartedEventArgs e) {
            IsThumbDragging = true;
        }

        private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e) {
            IsThumbDragging = false;
        }

        private static double RoundTo(double value, double precision = 1d) {
            if (Equals(precision, 0d)) return value;
            return Math.Round(value / precision) * precision;
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e) {
            var position = Mouse.GetPosition(_wrapper);
            position.X -= _wrapper.ActualWidth / 2d;
            position.Y -= _wrapper.ActualHeight / 2d;

            var angle = Math.Atan2(Math.Abs(position.X), position.X > 0 ? -position.Y : position.Y) / Math.PI + (position.X > 0 ? 1d : 0d);
            var value = Minimum + (Maximum - Minimum) * angle / 2d;
            Value = IsSnapToTickEnabled ? RoundTo(value, TickFrequency) : value;
        }

        private void OnWrapperMouseWheel(object sender, MouseWheelEventArgs e) {
            Value += IsSnapToTickEnabled ? (e.Delta < 0 ? -TickFrequency : +TickFrequency)
                    : (Maximum - Minimum) * e.Delta / 100d;
        }

        private class InnerConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                var value = values[0].As<double>();
                var min = values[1].As<double>();
                var max = values[2].As<double>();
                return 359.999 * (value - min) / (max - min);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IMultiValueConverter Converter { get; } = new InnerConverter();
    }
}