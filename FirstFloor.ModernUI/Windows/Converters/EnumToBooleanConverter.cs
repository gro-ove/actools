using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class EnumToBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (parameter == null) {
                return value == null;
            }

            var s = parameter.ToString();
            var i = s.Length > 0 && s[0] == '≠';
            var f = value?.ToString() == (i ? s.Substring(1) : s);
            if (i) {
                f = !f;
            }

            return f;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}