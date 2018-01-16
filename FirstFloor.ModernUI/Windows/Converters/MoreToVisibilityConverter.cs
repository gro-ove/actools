using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(double), typeof(Visibility))]
    public class MoreToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null) return null;

            var a = value.As<double>();
            if (parameter == null) {
                return a > 0d ? Visibility.Visible : Visibility.Collapsed;
            }

            var t = parameter.ToString();
            if (t == @"hidden") {
                return a > 0d ? Visibility.Hidden : Visibility.Collapsed;
            }

            if (!t.Contains(',')) {
                return a > t.As<double>() ? Visibility.Visible : Visibility.Collapsed;
            }

            var p = t.Split(',');
            var s = p[0].As<double>();
            var r = p.Contains(@"inverse") ? s > a : a > s;
            return r ? Visibility.Visible : p.Contains(@"hidden") ? Visibility.Hidden : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}