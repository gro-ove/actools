using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BooleanLazySwitch : BaseSwitch {
        protected override UIElement GetChild() {
            var v = Value;
            var key = v ? TrueResourceKey : FalseResourceKey;
            var format = v ? TrueResourceKeyStringFormat : FalseResourceKeyStringFormat;

            if (format != null) {
                key = string.Format(format, key);
            }

            var result = key == null ? null : TryFindResource(key) as UIElement;
            if (CollapseIfMissing) {
                Visibility = result == null ? Visibility.Collapsed : Visibility.Visible;
            }

            return result;
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(false, OnChildDefiningPropertyChanged));

        public bool Value {
            get => GetValue(ValueProperty) as bool? == true;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty TrueResourceKeyProperty = DependencyProperty.Register(nameof(TrueResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object TrueResourceKey {
            get => GetValue(TrueResourceKeyProperty);
            set => SetValue(TrueResourceKeyProperty, value);
        }

        public static readonly DependencyProperty FalseResourceKeyProperty = DependencyProperty.Register(nameof(FalseResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object FalseResourceKey {
            get => GetValue(FalseResourceKeyProperty);
            set => SetValue(FalseResourceKeyProperty, value);
        }

        public static readonly DependencyProperty TrueResourceKeyStringFormatProperty = DependencyProperty.Register(nameof(TrueResourceKeyStringFormat),
                typeof(string), typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public string TrueResourceKeyStringFormat {
            get => (string)GetValue(TrueResourceKeyStringFormatProperty);
            set => SetValue(TrueResourceKeyStringFormatProperty, value);
        }

        public static readonly DependencyProperty FalseResourceKeyStringFormatProperty = DependencyProperty.Register(nameof(FalseResourceKeyStringFormat),
                typeof(string), typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public string FalseResourceKeyStringFormat {
            get => (string)GetValue(FalseResourceKeyStringFormatProperty);
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

    [ContentProperty(nameof(Definitions))]
    public class LazySwitch : BaseSwitch {
        public LazySwitch() {
            Definitions = new ObservableCollection<LazySwitchDefinition>();
        }

        protected override UIElement GetChild() {
            var value = Value;
            var key = (Definitions.FirstOrDefault(x => x.When.XamlEquals(value)) ?? Definitions.FirstOrDefault(x => x.When == null))?.ResourceKey;
            return key == null ? null : TryFindResource(key) as UIElement;
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(LazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        [CanBeNull]
        public object Value {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty DefinitionsProperty = DependencyProperty.Register(nameof(Definitions),
                typeof(ObservableCollection<LazySwitchDefinition>),
                typeof(LazySwitch));

        public ObservableCollection<LazySwitchDefinition> Definitions {
            get => (ObservableCollection<LazySwitchDefinition>)GetValue(DefinitionsProperty);
            set => SetValue(DefinitionsProperty, value);
        }
    }

    public class LazySwitchDefinition {
        [CanBeNull]
        public object When { get; set; }

        [CanBeNull]
        public object ResourceKey { get; set; }
    }
}