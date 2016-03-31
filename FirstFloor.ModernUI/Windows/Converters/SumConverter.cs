using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class SumConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null) return null;
            var numValue = System.Convert.ToInt32(value);
            return numValue + System.Convert.ToInt32(parameter);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public class SubstractConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null) return null;
            var numValue = System.Convert.ToDouble(value);
            return numValue - System.Convert.ToDouble(parameter);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public class EqualToBooleanConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            var first = values.FirstOrDefault()?.ToString();
            return values.Skip(1).All(x => Equals(first, x?.ToString()));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
