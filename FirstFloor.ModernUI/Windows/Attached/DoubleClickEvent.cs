using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class DoubleClickEvent {
        public static bool GetEnabled(DependencyObject obj) {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value) {
            obj.SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool),
                typeof(DoubleClickEvent), new UIPropertyMetadata(OnEnabledChanged));

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as FrameworkElement;
            if (element == null || !(e.NewValue is bool)) return;

            var newValue = (bool)e.NewValue;
            if (newValue) {
                element.PreviewMouseLeftButtonDown += OnClick;
            } else {
                element.PreviewMouseLeftButtonDown -= OnClick;
            }
        }

        private static void OnClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                var element = sender as FrameworkElement;
                if (element != null) {
                    var cmd = element.InputBindings.OfType<MouseBinding>()
                           .FirstOrDefault(x => (x.Gesture as MouseGesture)?.MouseAction == MouseAction.LeftDoubleClick)?.Command;
                    cmd?.Execute(null);
                }
            }
        }
    }
}