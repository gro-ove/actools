using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(double), typeof(double))]
    public class SubstractConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value.AsDouble() - parameter.AsDouble();
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}