using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return null;

            long number;
            if (!long.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number)) {
                number = 0;
            }

            return number.ToReadableSize();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}