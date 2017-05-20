using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FirstFloor.ModernUI.Windows.Attached {
    public class VisibilityAnimation : DependencyObject {
        public static TimeSpan GetDuration(DependencyObject obj) {
            return (TimeSpan)obj.GetValue(DurationProperty);
        }

        public static void SetDuration(DependencyObject obj, TimeSpan value) {
            obj.SetValue(DurationProperty, value);
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.RegisterAttached("Duration", typeof(TimeSpan),
                typeof(VisibilityAnimation), new UIPropertyMetadata(TimeSpan.FromSeconds(0.3)));

        public static bool? GetVisible(DependencyObject obj) {
            return (bool?)obj.GetValue(VisibleProperty);
        }

        public static void SetVisible(DependencyObject obj, bool? value) {
            obj.SetValue(VisibleProperty, value);
        }

        public static readonly DependencyProperty VisibleProperty = DependencyProperty.RegisterAttached("Visible", typeof(bool?),
                typeof(VisibilityAnimation), new UIPropertyMetadata(null, OnVisibleChanged));

        private static void OnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as UIElement;
            if (element == null || !(e.NewValue is bool?)) return;

            var oldValue = e.OldValue as bool?;
            var newValue = (bool?)e.NewValue == true;
            if (!oldValue.HasValue) {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                if (!newValue) {
                    element.Opacity = 0d;
                    element.Visibility = Visibility.Hidden;
                }
                return;
            }

            if (oldValue == newValue) {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                return;
            }

            var da = new DoubleAnimation {
                Duration = new Duration(GetDuration(element)),
                To = newValue ? 1d : 0d
            };

            if (da.To == element.Opacity) {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                return;
            }

            if (element.Visibility == Visibility.Hidden) {
                element.Visibility = Visibility.Visible;
            }

            if (!newValue) {
                da.Completed += (o, ev) => {
                    element.Visibility = Visibility.Hidden;
                };
            }

            element.BeginAnimation(UIElement.OpacityProperty, da);
        }
    }
}