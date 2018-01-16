using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MultiLessToBooleanConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length != 2) return false;
            return values[0].As<double>() + parameter.As<double>() < values[1].As<double>();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}