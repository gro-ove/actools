using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(double), typeof(double))]
    public class PercentageConverter : IValueConverter {
        private static double Round(double value, double precision) {
            return Math.Round(value / precision) * precision;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round(value.As<double>() * 1000000) / 10000;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter == null ? value.As<double>() / 100 : Round(value.As<double>(), parameter.As<double>()) / 100;
        }
    }
}