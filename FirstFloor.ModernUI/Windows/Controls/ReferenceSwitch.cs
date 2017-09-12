using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(NonNull))]
    public class ReferenceSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object Value {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty NullProperty = DependencyProperty.Register(nameof(Null), typeof(UIElement),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement Null {
            get => (UIElement)GetValue(NullProperty);
            set => SetValue(NullProperty, value);
        }

        public static readonly DependencyProperty NonNullProperty = DependencyProperty.Register(nameof(NonNull), typeof(UIElement),
                typeof(ReferenceSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement NonNull {
            get => (UIElement)GetValue(NonNullProperty);
            set => SetValue(NonNullProperty, value);
        }

        protected override UIElement GetChild() {
            return Value == null ? Null : NonNull;
        }
    }
}