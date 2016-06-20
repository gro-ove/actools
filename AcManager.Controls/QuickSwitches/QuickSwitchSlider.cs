using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Controls.QuickSwitches {
    public class QuickSwitchSlider : Slider {
        static QuickSwitchSlider() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickSwitchSlider), new FrameworkPropertyMetadata(typeof(QuickSwitchSlider)));
        }

        public static readonly DependencyProperty IsThumbDraggingProperty = DependencyProperty.Register(nameof(IsThumbDragging), typeof(bool),
                typeof(QuickSwitchSlider));

        public bool IsThumbDragging {
            get { return (bool)GetValue(IsThumbDraggingProperty); }
            set { SetValue(IsThumbDraggingProperty, value); }
        }

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry),
                typeof(QuickSwitchSlider));

        public Geometry IconData {
            get { return (Geometry)GetValue(IconDataProperty); }
            set { SetValue(IconDataProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(string),
                typeof(QuickSwitchSlider));

        public string Content {
            get { return (string)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(nameof(DisplayValue), typeof(string),
                typeof(QuickSwitchSlider));

        public string DisplayValue {
            get { return (string)GetValue(DisplayValueProperty); }
            set { SetValue(DisplayValueProperty, value); }
        }

        private Thumb _thumb;
        private FrameworkElement _wrapper;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_thumb != null) {
                _thumb.DragDelta -= Thumb_DragDelta;
                _thumb.DragStarted -= Thumb_DragStarted;
                _thumb.DragCompleted -= Thumb_DragCompleted;
            }

            if (_wrapper != null) {
                _wrapper.MouseWheel -= Wrapper_MouseWheel;
            }

            _thumb = GetTemplateChild("PART_Thumb") as Thumb;
            _wrapper = GetTemplateChild("PART_Wrapper") as FrameworkElement;

            if (_thumb != null) {
                _thumb.DragDelta += Thumb_DragDelta;
                _thumb.DragStarted += Thumb_DragStarted;
                _thumb.DragCompleted += Thumb_DragCompleted;
            }

            if (_wrapper != null) {
                _wrapper.MouseWheel += Wrapper_MouseWheel;
            }
        }

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e) {
            IsThumbDragging = true;
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e) {
            IsThumbDragging = false;
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e) {
            var position = Mouse.GetPosition(_wrapper);
            position.X -= _wrapper.ActualWidth / 2d;
            position.Y -= _wrapper.ActualHeight / 2d;
            
            var angle = Math.Atan2(Math.Abs(position.X), position.X > 0 ? -position.Y : position.Y) / Math.PI + (position.X > 0 ? 1d : 0d);
            var value = Minimum + (Maximum - Minimum) * angle / 2d;
            Value = IsSnapToTickEnabled ? value.Round(TickFrequency) : value;
        }

        private void Wrapper_MouseWheel(object sender, MouseWheelEventArgs e) {
            Value += IsSnapToTickEnabled ? (e.Delta < 0 ? -TickFrequency : +TickFrequency)
                    : (Maximum - Minimum) * e.Delta / 100d;
        }

        private class InnerConverter : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                var value = values[0].AsDouble();
                var min = values[1].AsDouble();
                var max = values[2].AsDouble();
                return 359.999 * (value - min) / (max - min);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IMultiValueConverter Converter { get; } = new InnerConverter();
    }
}