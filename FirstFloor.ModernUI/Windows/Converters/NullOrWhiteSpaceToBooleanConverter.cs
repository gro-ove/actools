using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(string), typeof(bool))]
    public class NullOrWhiteSpaceToBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var flag = string.IsNullOrWhiteSpace(value?.ToString());
            var parameterString = parameter as string ?? "";
            var inverse = parameterString.Contains(@"inverse");
            return inverse ? !flag : flag;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}