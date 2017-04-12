using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(double), typeof(double))]
    public class PercentageConverter : IValueConverter {
        private static double Round(double value, double precision) {
            return Math.Round(value / precision) * precision;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return Math.Round(value.AsDouble() * 1000000) / 10000;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter == null ? value.AsDouble() / 100 : Round(value.AsDouble(), parameter.AsDouble()) / 100;
        }
    }
}