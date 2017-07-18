using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(long), typeof(string))]
    public class FileSizeConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value?.AsLong().ToReadableSize();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return LocalizationHelper.TryParseReadableSize(value?.ToString(), parameter?.ToString() ?? "MB", out long parsed) ? parsed : 0;
        }
    }
}