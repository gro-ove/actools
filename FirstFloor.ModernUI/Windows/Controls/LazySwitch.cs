using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(Definitions))]
    public class LazySwitch : BaseLazySwitch {
        public LazySwitch() {
            _definitions = new ObservableCollection<LazySwitchDefinition>();
            SetValue(DefinitionsProperty, _definitions);
        }

        protected override UIElement GetChild() {
            var value = _value;
            var key = (_definitions.FirstOrDefault(x => x.When.XamlEquals(value)) ?? _definitions.FirstOrDefault(x => x.When == null))?.ResourceKey;
            return GetChildFromResources(key);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(object),
                typeof(LazySwitch), new PropertyMetadata(null, (o, e) => {
                    ((LazySwitch)o)._value = e.NewValue;
                    OnChildAffectingPropertyChanged(o, e);
                }));

        private object _value;

        public object Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty DefinitionsProperty = DependencyProperty.Register(nameof(Definitions), typeof(ObservableCollection<LazySwitchDefinition>),
                typeof(LazySwitch));

        private readonly ObservableCollection<LazySwitchDefinition> _definitions;

        public ObservableCollection<LazySwitchDefinition> Definitions {
            get => _definitions;
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