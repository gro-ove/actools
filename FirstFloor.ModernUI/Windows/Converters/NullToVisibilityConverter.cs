using System;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var flag = value == null;
            var parameterString = parameter as string ?? "";
            var inverse = parameterString.Contains(@"inverse");
            var hidden = parameterString.Contains(@"hidden");
            return (inverse ? !flag : flag) ? Visibility.Visible : hidden ? Visibility.Hidden : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
