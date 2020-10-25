using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Windows.Converters;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class FallbackSwitch : ListSwitch {
        public static object GetValue(DependencyObject obj) {
            return obj.GetValue(ValueProperty);
        }

        public static void SetValue(DependencyObject obj, object value) {
            obj.SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached("Value", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        public static object GetWhen(DependencyObject obj) {
            return obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, object value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        private static readonly object UnsetValue = new object();

        public static object GetWhenNot(DependencyObject obj) {
            return obj.GetValue(WhenNotProperty);
        }

        public static void SetWhenNot(DependencyObject obj, object value) {
            obj.SetValue(WhenNotProperty, value);
        }

        public static readonly DependencyProperty WhenNotProperty = DependencyProperty.RegisterAttached("WhenNot", typeof(object),
                typeof(FallbackSwitch), new FrameworkPropertyMetadata(UnsetValue, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnWhenChanged));

        protected override bool TestChild(UIElement child) {
            var value = GetValue(child);
            var whenNot = GetWhenNot(child);
            // Logging.Debug($"{child}, {(child as FrameworkElement)?.Name}, value={value}, when={GetWhen(child)}, result={value.XamlEquals(GetWhen(child))}");
            if (!ReferenceEquals(whenNot, UnsetValue)) {
                return !value.XamlEquals(whenNot);
            }
            return value.XamlEquals(GetWhen(child));
        }

        protected override UIElement GetChild() {
            return UiElements?.FirstOrDefault(TestChild);
        }
    }
}