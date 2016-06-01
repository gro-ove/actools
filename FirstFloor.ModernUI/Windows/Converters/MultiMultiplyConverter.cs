using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MultiMultiplyConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length == 0) return 0d;
            var result = values[0].AsDouble();
            for (var i = 1; i < values.Length; i++) {
                result *= values[i].AsDouble();
            }
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}