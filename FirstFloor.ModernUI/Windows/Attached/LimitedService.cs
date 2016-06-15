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
            // TODO: dynamic change?
            if (OptionNonLimited) return;

            var u = d as FrameworkElement;
            if (u == null) return;

            u.Loaded += Control_Loaded;
        }

        private static void Control_Loaded(object sender, RoutedEventArgs e) {
            if (OptionNonLimited) return;

            var c = (UIElement)sender;
            if (GetLimited(c)) {
                Add(c);
            }
        }

        private static void Add(UIElement control) {
            AdornerLayer.GetAdornerLayer(control)?.Add(new LimitedAdorner(control));
        }

        private static void Remove(UIElement control) {
            var layer = AdornerLayer.GetAdornerLayer(control);

            var adorners = layer?.GetAdorners(control);
            if (adorners == null) return;

            foreach (var adorner in adorners.OfType<LimitedAdorner>()) {
                adorner.Visibility = Visibility.Hidden;
                layer.Remove(adorner);
            }
        }
    }
}