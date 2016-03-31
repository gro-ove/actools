using System;
using System.Windows.Data;
using System.Globalization;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class BooleanToTextConverter
        : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var flag = false;
            if (value is bool) {
                flag = (bool)value;
            }

            if (parameter as string == "inverse") {
                flag = !flag;
            }

            return flag ? "Yes" : "No";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
