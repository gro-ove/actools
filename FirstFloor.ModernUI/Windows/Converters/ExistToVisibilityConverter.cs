using System;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class ExistToVisibilityConverter
        : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var path = value?.ToString();
            var flag = !string.IsNullOrEmpty(path) && File.Exists(path);
            if (parameter as string == "inverse") {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException("Two way conversion is not supported.");
        }
    }
}
