using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using FirstFloor.ModernUI.Windows.Converters;

namespace FirstFloor.ModernUI.Windows.Bindings {
    [ContentProperty(nameof(Values))]
    public class MapBinding : Binding, IValueConverter {
        public MapBinding() {
            Initialize();
        }

        public MapBinding(string path) : base(path) {
            Initialize();
        }

        public MapValues Values { get; set; } = new MapValues();

        private void Initialize() {
            Converter = this;
            ConverterParameter = Values;
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (parameter is MapValues values) {
                foreach (var mapValue in values) {
                    if (value.XamlEquals(mapValue.Key)) {
                        return mapValue.Value;
                    }
                }
            }
            return null;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (parameter is MapValues values) {
                foreach (var mapValue in values) {
                    if (value.XamlEquals(mapValue.Value)) {
                        return mapValue.Key;
                    }
                }
            }
            return null;
        }
    }
}