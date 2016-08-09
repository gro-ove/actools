using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(double), typeof(bool))]
    public class LessToBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value.AsDouble() < parameter.AsDouble();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public class MultiLessToBooleanConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length != 2) return false;
            return values[0].AsDouble() + parameter.AsDouble() < values[1].AsDouble();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}