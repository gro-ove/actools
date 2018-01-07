using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Localizable;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(string), typeof(string))]
    public class ToTitleConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value == null ? null : Titling.Convert(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value;
        }
    }
}
