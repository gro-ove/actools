using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BooleanLazySwitch : BaseLazySwitch {
        protected override UIElement GetChild() {
            var v = _value;
            var key = v ? TrueResourceKey : FalseResourceKey;
            var format = v ? TrueResourceKeyStringFormat : FalseResourceKeyStringFormat;

            if (format != null) {
                key = string.Format(format, key);
            }

            var result = GetChildFromResources(key);
            if (_collapseIfMissing) {
                var targetVisibility = result == null ? Visibility.Collapsed : Visibility.Visible;
                if (targetVisibility != Visibility) {
                    Visibility = targetVisibility;
                }
            }

            return result;
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanLazySwitch), new PropertyMetadata(false, (o, e) => {
                    ((BooleanLazySwitch)o)._value = (bool)e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private bool _value;

        public bool Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty TrueResourceKeyProperty = DependencyProperty.Register(nameof(TrueResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanLazySwitch)o)._trueResourceKey = e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private object _trueResourceKey;

        public object TrueResourceKey {
            get => _trueResourceKey;
            set => SetValue(TrueResourceKeyProperty, value);
        }

        public static readonly DependencyProperty FalseResourceKeyProperty = DependencyProperty.Register(nameof(FalseResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanLazySwitch)o)._falseResourceKey = e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private object _falseResourceKey;

        public object FalseResourceKey {
            get => _falseResourceKey;
            set => SetValue(FalseResourceKeyProperty, value);
        }

        public static readonly DependencyProperty TrueResourceKeyStringFormatProperty = DependencyProperty.Register(nameof(TrueResourceKeyStringFormat), typeof(string),
                typeof(BooleanLazySwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanLazySwitch)o)._trueResourceKeyStringFormat = (string)e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private string _trueResourceKeyStringFormat;

        public string TrueResourceKeyStringFormat {
            get => _trueResourceKeyStringFormat;
            set => SetValue(TrueResourceKeyStringFormatProperty, value);
        }

        public static readonly DependencyProperty FalseResourceKeyStringFormatProperty = DependencyProperty.Register(nameof(FalseResourceKeyStringFormat), typeof(string),
                typeof(BooleanLazySwitch), new PropertyMetadata(null, (o, e) => {
                    ((BooleanLazySwitch)o)._falseResourceKeyStringFormat = (string)e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private string _falseResourceKeyStringFormat;

        public string FalseResourceKeyStringFormat {
            get => _falseResourceKeyStringFormat;
            set => SetValue(FalseResourceKeyStringFormatProperty, value);
        }

        public static readonly DependencyProperty CollapseIfMissingProperty = DependencyProperty.Register(nameof(CollapseIfMissing), typeof(bool),
                typeof(BooleanLazySwitch), new PropertyMetadata(false, (o, e) => { ((BooleanLazySwitch)o)._collapseIfMissing = (bool)e.NewValue; }));

        private bool _collapseIfMissing;

        public bool CollapseIfMissing {
            get => _collapseIfMissing;
            set => SetValue(CollapseIfMissingProperty, value);
        }
    }
}