using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class ComboBoxAdvancement {
        public static IValueConverter GetDisplayConverter(DependencyObject obj) {
            return (IValueConverter)obj.GetValue(DisplayConverterProperty);
        }

        public static void SetDisplayConverter(DependencyObject obj, IValueConverter value) {
            obj.SetValue(DisplayConverterProperty, value);
        }

        public static readonly DependencyProperty DisplayConverterProperty = DependencyProperty.RegisterAttached("DisplayConverter", typeof(IValueConverter),
                typeof(ComboBoxAdvancement), new UIPropertyMetadata(OnDisplayConverterChanged));

        private static void OnDisplayConverterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as ComboBox;
            if (element == null || !(e.NewValue is IValueConverter)) return;

            var newValue = (IValueConverter)e.NewValue;
            if (newValue == null) {
                element.ItemTemplate = null;
            } else {
                var visualTree = new FrameworkElementFactory(typeof(TextBlock));
                visualTree.SetBinding(TextBlock.TextProperty, new Binding {
                    Converter = newValue
                });

                element.ItemTemplate = new DataTemplate {
                    VisualTree = visualTree
                };
            }
        }
    }

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