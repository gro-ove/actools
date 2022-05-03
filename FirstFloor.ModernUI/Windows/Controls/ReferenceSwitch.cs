using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(NonNull))]
    public class ReferenceSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(ReferenceSwitch), new PropertyMetadata(null, (o, e) => {
                    ((ReferenceSwitch)o)._value = e.NewValue;
                    OnChildSelectingPropertyChanged(o, e);
                }));

        private object _value;

        public object Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty NullProperty = DependencyProperty.Register(nameof(Null), typeof(UIElement),
                typeof(ReferenceSwitch), new PropertyMetadata(null, (o, e) => {
                    ((ReferenceSwitch)o)._null = (UIElement)e.NewValue;
                    OnChildRegisteringPropertyChanged(o, e);
                }));

        private UIElement _null;

        public UIElement Null {
            get => _null;
            set => SetValue(NullProperty, value);
        }

        public static readonly DependencyProperty NonNullProperty = DependencyProperty.Register(nameof(NonNull), typeof(UIElement),
                typeof(ReferenceSwitch), new PropertyMetadata(null, (o, e) => {
                    ((ReferenceSwitch)o)._nonNull = (UIElement)e.NewValue;
                    OnChildRegisteringPropertyChanged(o, e);
                }));

        private UIElement _nonNull;

        public UIElement NonNull {
            get => _nonNull;
            set => SetValue(NonNullProperty, value);
        }

        protected override UIElement GetChild() {
            return _value == null ? _null : _nonNull;
        }
    }
}