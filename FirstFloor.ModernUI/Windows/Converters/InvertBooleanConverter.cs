using System;
using System.Windows.Data;
using System.Globalization;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InvertBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is bool && false == (bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is bool && false == (bool)value;
        }
    }
}
