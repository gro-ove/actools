using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(True))]
    public class BooleanSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanSwitch), new PropertyMetadata(false, (o, e) => {
                    ((BooleanSwitch)o)._value = (bool)e.NewValue;
                    OnChildSelectingPropertyChanged(o, e);
                }));

        private bool _value;

        public bool Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty TrueProperty = DependencyProperty.Register(nameof(True), typeof(UIElement),
                typeof(BooleanSwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanSwitch)o)._true = (UIElement)e.NewValue;
                    OnChildRegisteringPropertyChanged(o, e);
                }));

        private UIElement _true;

        public UIElement True {
            get => _true;
            set => SetValue(TrueProperty, value);
        }

        public static readonly DependencyProperty FalseProperty = DependencyProperty.Register(nameof(False), typeof(UIElement),
                typeof(BooleanSwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanSwitch)o)._false = (UIElement)e.NewValue;
                    OnChildRegisteringPropertyChanged(o, e);
                }));

        private UIElement _false;

        public UIElement False {
            get => _false;
            set => SetValue(FalseProperty, value);
        }

        protected override UIElement GetChild() {
            var value = _value;
            if (_collapseOnFalse) {
                var targetVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (Visibility != targetVisibility) {
                    Visibility = targetVisibility;
                }
            }
            return value ? _true : _false;
        }

        public static readonly DependencyProperty CollapseOnFalseProperty = DependencyProperty.Register(nameof(CollapseOnFalse), typeof(bool),
                typeof(BooleanSwitch), new PropertyMetadata(false, (o, e) => {
                    var b = (BooleanSwitch)o;
                    b._collapseOnFalse = (bool)e.NewValue;
                    if (b._collapseOnFalse && !b.Value) {
                        b.Visibility = Visibility.Collapsed;
                    }
                }));

        private bool _collapseOnFalse;

        public bool CollapseOnFalse {
            get => _collapseOnFalse;
            set => SetValue(CollapseOnFalseProperty, value);
        }
    }
}