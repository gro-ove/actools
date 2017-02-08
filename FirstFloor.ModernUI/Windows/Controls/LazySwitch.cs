using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BooleanLazySwitch : BaseSwitch {
        protected override UIElement GetChild() {
            var key = Value ? TrueResourceKey : FalseResourceKey;
            return key == null ? null : TryFindResource(key) as UIElement;
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(bool),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(false, OnChildDefiningPropertyChanged));

        public bool Value {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty TrueResourceKeyProperty = DependencyProperty.Register(nameof(TrueResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object TrueResourceKey {
            get { return GetValue(TrueResourceKeyProperty); }
            set { SetValue(TrueResourceKeyProperty, value); }
        }

        public static readonly DependencyProperty FalseResourceKeyProperty = DependencyProperty.Register(nameof(FalseResourceKey), typeof(object),
                typeof(BooleanLazySwitch), new FrameworkPropertyMetadata(null, OnChildDefiningPropertyChanged));

        public object FalseResourceKey {
            get { return GetValue(FalseResourceKeyProperty); }
            set { SetValue(FalseResourceKeyProperty, value); }
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
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty DefinitionsProperty = DependencyProperty.Register(nameof(Definitions), typeof(ObservableCollection<LazySwitchDefinition>),
                typeof(LazySwitch));

        public ObservableCollection<LazySwitchDefinition> Definitions {
            get { return (ObservableCollection<LazySwitchDefinition>)GetValue(DefinitionsProperty); }
            set { SetValue(DefinitionsProperty, value); }
        }
    }

    public class LazySwitchDefinition {
        [CanBeNull]
        public object When { get; set; }

        [CanBeNull]
        public object ResourceKey { get; set; }
    }
}