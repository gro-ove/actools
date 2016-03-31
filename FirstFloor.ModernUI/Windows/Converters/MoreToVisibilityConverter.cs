using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class MoreToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null) return null;

            double a;
            double.TryParse(value.ToString(), out a);

            if (parameter == null) {
                return a > 0d ? Visibility.Visible : Visibility.Collapsed;
            }

            var ps = parameter.ToString();

            double s;
            if (!ps.Contains(',')) {
                double.TryParse(ps, out s);
                return a > s ? Visibility.Visible : Visibility.Collapsed;
            }

            var p = ps.Split(',');
            double.TryParse(p[0], out s);
            var inverse = p.Contains("inverse");

            var result = inverse ? s > a : a > s;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}