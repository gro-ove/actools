using System;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MoreConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return value.AsDouble() > parameter.AsDouble();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
