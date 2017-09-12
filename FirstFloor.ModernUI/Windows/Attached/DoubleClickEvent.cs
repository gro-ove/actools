using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class DoubleClickEvent {
        public static bool GetEnabled(DependencyObject obj) {
            return obj.GetValue(EnabledProperty) as bool? == true;
        }

        public static void SetEnabled(DependencyObject obj, bool value) {
            obj.SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool),
                typeof(DoubleClickEvent), new UIPropertyMetadata(OnEnabledChanged));

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement element) || !(e.NewValue is bool)) return;
            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.PreviewMouseLeftButtonDown += OnClick;
            } else {
                element.PreviewMouseLeftButtonDown -= OnClick;
            }
        }

        private static void OnClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 && sender is FrameworkElement element) {
                var cmd = element.InputBindings.OfType<MouseBinding>()
                                 .FirstOrDefault(x => (x.Gesture as MouseGesture)?.MouseAction == MouseAction.LeftDoubleClick)?.Command;
                cmd?.Execute(null);
            }
        }
    }
}