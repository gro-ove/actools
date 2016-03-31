using System;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MoreConverter
        : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null) return null;

            double a, b;
            return double.TryParse(value.ToString(), out a) && double.TryParse(parameter?.ToString() ?? "0", out b) && a > b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
