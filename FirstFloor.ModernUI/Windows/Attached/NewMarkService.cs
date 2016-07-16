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
            return (bool)d.GetValue(NewProperty);
        }

        public static void SetNew(DependencyObject d, bool value) {
            d.SetValue(NewProperty, value);
        }

        private static void OnLimitedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var u = d as FrameworkElement;
            if (u == null) return;

            u.Loaded += Control_Loaded;
        }
        
        private static void Control_Loaded(object sender, RoutedEventArgs e) {
            var c = (UIElement)sender;
            if (GetNew(c)) {
                AdornerLayer.GetAdornerLayer(c)?.Add(new NewMarkAdorner(c));
            }
        }
    }
}