using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class AndConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values.All(x => x.As<bool>());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public class OrConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            return values.Any(x => x.As<bool>());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}