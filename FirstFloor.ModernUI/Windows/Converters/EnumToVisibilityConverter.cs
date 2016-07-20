using System;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class EnumToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (parameter == null) {
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            }

            var s = parameter.ToString();
            var i = s.Length > 0 && s[0] == '≠';
            var f = value?.ToString() == (i ? s.Substring(1) : s);
            if (i) {
                f = !f;
            }

            return f ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
