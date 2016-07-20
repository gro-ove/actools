using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(int), typeof(string))]
    public class OrdinalizingConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value.AsInt().ToOrdinal(parameter as string, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}