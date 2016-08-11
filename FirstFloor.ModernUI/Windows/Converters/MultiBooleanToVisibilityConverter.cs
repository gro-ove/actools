using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MultiBooleanToVisibilityConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            var flag = values.All(x => x is bool && (bool)x);
            var parameterString = parameter as string ?? "";
            var inverse = parameterString.Contains(@"inverse");
            var hidden = parameterString.Contains(@"hidden");
            return (inverse ? !flag : flag) ? Visibility.Visible : hidden ? Visibility.Hidden : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}