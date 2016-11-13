using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class EqualToBooleanConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length < 2) return false;
            var first = values[0]?.ToString();
            return values.Skip(1).All(x => Equals(first, x?.ToString()));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}