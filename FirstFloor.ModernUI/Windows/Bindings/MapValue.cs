using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Bindings {
    public class MapValue : NotifyPropertyChanged {
        private object _key;

        public object Key {
            get => _key;
            set => Apply(value, ref _key);
        }

        private object _value;

        public object Value {
            get => _value;
            set => Apply(value, ref _value);
        }
    }
}