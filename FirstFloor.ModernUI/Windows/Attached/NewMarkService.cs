using System.Windows;
using System.Windows.Documents;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class NewMarkService {
        public static readonly DependencyProperty NewProperty;

        static NewMarkService() {
            NewProperty = DependencyProperty.RegisterAttached("New", typeof(bool), typeof(NewMarkService),
                    new FrameworkPropertyMetadata(false, OnLimitedChanged));
        }

        public static bool GetNew(DependencyObject d) {
            return d.GetValue(NewProperty) as bool? == true;
        }

        public static void SetNew(DependencyObject d, bool value) {
            d.SetValue(NewProperty, value);
        }

        private static void OnLimitedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is FrameworkElement u)) return;
            u.Loaded += OnControlLoaded;
        }

        private static void OnControlLoaded(object sender, RoutedEventArgs e) {
            var c = (UIElement)sender;
            if (GetNew(c)) {
                AdornerLayer.GetAdornerLayer(c)?.Add(new NewMarkAdorner(c));
            }
        }
    }
}