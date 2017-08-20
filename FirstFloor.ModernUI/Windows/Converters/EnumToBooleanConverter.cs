using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(object), typeof(bool))]
    public class EnumToBooleanConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (parameter == null) return value == null;
            var s = parameter.ToString();
            var i = s.Length > 0 && s[0] == '≠';
            var f = value?.ToString() == (i ? s.Substring(1) : s);
            return i ? !f : f;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null || parameter == null) return null;
            var useValue = (bool)value;
            var targetValue = parameter.ToString();
            return useValue ? Enum.Parse(targetType, targetValue) : null;
        }
    }
}