using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var flag = value == null;
            var parameterString = parameter as string ?? "";
            var inverse = parameterString.Contains(@"inverse");
            return inverse ? !flag : flag;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}