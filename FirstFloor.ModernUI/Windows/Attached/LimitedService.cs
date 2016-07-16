using System.Linq;
using System.Windows;
using System.Windows.Documents;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class LimitedService {
        public static bool OptionNonLimited = false;

        public static readonly DependencyProperty LimitedProperty;

        static LimitedService() {
            LimitedProperty = DependencyProperty.RegisterAttached("Limited", typeof(bool), typeof(LimitedService),
                    new FrameworkPropertyMetadata(false, OnLimitedChanged));
        }

        public static bool GetLimited(DependencyObject d) {
            return (bool)d.GetValue(LimitedProperty);
        }

        public static void SetLimited(DependencyObject d, bool value) {
            d.SetValue(LimitedProperty, value);
        }

        private static void OnLimitedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (OptionNonLimited) return;

            var u = d as FrameworkElement;
            if (u != null) {
                u.Loaded += Control_Loaded;
            }
        }

        private static void Control_Loaded(object sender, RoutedEventArgs e) {
            if (OptionNonLimited) return;

            var c = (FrameworkElement)sender;
            c.Loaded -= Control_Loaded;
            if (GetLimited(c)) {
                AdornerLayer.GetAdornerLayer(c)?.Add(new LimitedMarkAdorner(c));
                c.IsEnabled = false;
            }
        }
    }
}