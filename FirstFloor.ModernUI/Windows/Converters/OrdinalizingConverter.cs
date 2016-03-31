using System;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class OrdinalizingConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            int number;
            if (value == null || !int.TryParse(value.ToString(), out number)) return value;

            var result = LocalizationHelper.GetOrdinalReadable(number);
            return (parameter as string)?.Contains("lower") == true ? result.ToLowerInvariant() : result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}