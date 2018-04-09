using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Bindings {
    [ContentProperty(nameof(TrueValue))]
    public class BooleanBinding : Binding, IValueConverter {
        public BooleanBinding() {
            Initialize();
        }

        public BooleanBinding(string path) : base(path) {
            Initialize();
        }

        public object TrueValue { get; set; }
        public object FalseValue { get; set; }

        private void Initialize() {
            Converter = this;
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (value.As<bool?>()) {
                case true:
                    return TrueValue;
                case false:
                    return FalseValue;
                default:
                    return null;
            }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (Equals(value, TrueValue)) return true;
            if (Equals(value, FalseValue)) return false;
            return null;
        }
    }
}