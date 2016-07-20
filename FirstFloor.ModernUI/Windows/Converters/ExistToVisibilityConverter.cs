using System;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class ExistToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var path = value?.ToString();
            var flag = !string.IsNullOrEmpty(path) && File.Exists(path);
            if (parameter as string == @"inverse") {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
