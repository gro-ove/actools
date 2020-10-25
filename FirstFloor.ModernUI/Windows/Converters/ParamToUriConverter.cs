using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(string), typeof(string))]
    public class ParamToUriConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var uri = parameter?.ToString();
            return string.IsNullOrWhiteSpace(uri) ? null : new Uri(uri, UriKind.RelativeOrAbsolute).AddQueryParam("param", value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}