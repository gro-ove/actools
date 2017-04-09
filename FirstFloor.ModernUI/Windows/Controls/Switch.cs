using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class Switch : ListSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(Switch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object Value {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static object GetWhen(DependencyObject obj) {
            return obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, object value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(object),
                typeof(Switch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        protected override bool TestChild(UIElement child) {
            // Logging.Debug($"{((FrameworkElement)child).Name}: {Value} ({Value?.GetType().Name ?? "<NULL>"}) == {GetWhen(child)} ({GetWhen(child)?.GetType().Name ?? "<NULL>"})");
            return Value.XamlEquals(GetWhen(child));
        }

        protected override UIElement GetChild() {
            return UiElements == null ? null : (UiElements.FirstOrDefault(TestChild) ??
                    UiElements.FirstOrDefault(x => x.GetValue(WhenProperty) == null));
        }
    }
}