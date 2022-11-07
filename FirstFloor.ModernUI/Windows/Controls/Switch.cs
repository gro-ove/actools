using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class Switch : ListSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(Switch), new PropertyMetadata(null, (o, e) => {
                    ((Switch)o)._value = e.NewValue;
                    OnChildSelectingPropertyChanged(o, e);
                }));

        private object _value;

        public object Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        public static object GetWhen(DependencyObject obj) {
            return obj.GetValue(WhenProperty);
        }

        public static void SetWhen(DependencyObject obj, object value) {
            obj.SetValue(WhenProperty, value);
        }

        public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached("When", typeof(object),
                typeof(Switch), new FrameworkPropertyMetadata(null, OnWhenChanged));

        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is UIElement element) {
                element.GetParent<BaseSwitch>()?.RefreshActiveChild();
            }
        }

        protected override bool TestChild(UIElement child) {
            return _value.XamlEquals(GetWhen(child));
        }

        protected override UIElement GetChild() {
            return RegisteredElements.FirstOrDefault(TestChild) ?? RegisteredElements.FirstOrDefault(x => x.GetValue(WhenProperty) == null);
        }
    }
}