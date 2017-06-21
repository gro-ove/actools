using System.Windows;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(True))]
    public class BooleanSwitch : BaseSwitch {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(false, OnChildDefiningPropertyChanged));

        public bool Value {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty TrueProperty = DependencyProperty.Register(nameof(True), typeof(UIElement),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement True {
            get { return (UIElement)GetValue(TrueProperty); }
            set { SetValue(TrueProperty, value); }
        }

        public static readonly DependencyProperty FalseProperty = DependencyProperty.Register(nameof(False), typeof(UIElement),
                typeof(BooleanSwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public UIElement False {
            get { return (UIElement)GetValue(FalseProperty); }
            set { SetValue(FalseProperty, value); }
        }

        protected override UIElement GetChild() {
            if (Value) {
                if (CollapseOnFalse) Visibility = Visibility.Visible;
                return True;
            }

            if (CollapseOnFalse) Visibility = Visibility.Collapsed;
            return False;
        }

        public static readonly DependencyProperty CollapseOnFalseProperty = DependencyProperty.Register(nameof(CollapseOnFalse), typeof(bool),
                typeof(BooleanSwitch), new PropertyMetadata(false, (o, e) => {
                    var b = ((BooleanSwitch)o);
                    b._collapseOnFalse = (bool)e.NewValue;
                    if (b._collapseOnFalse && !b.Value) {
                        b.Visibility = Visibility.Collapsed;
                    }
                }));

        private bool _collapseOnFalse;

        public bool CollapseOnFalse {
            get { return _collapseOnFalse; }
            set { SetValue(CollapseOnFalseProperty, value); }
        }
    }
}