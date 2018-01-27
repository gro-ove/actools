using System;
using System.Globalization;
using System.Windows.Data;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(double), typeof(double))]
    public class TemperatureToFahrenheitConverter : IValueConverter {
        public static readonly TemperatureToFahrenheitConverter Instance = new TemperatureToFahrenheitConverter();

        public static double ToFahrenheit(double celsius) {
            return celsius * 1.8 + 32;
        }

        public static double ToCelsius(double fahrenheit) {
            return (fahrenheit - 32) / 1.8;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter as string == "relative" ? value.As<double>() * 1.8 : ToFahrenheit(value.As<double>());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter as string == "relative" ? value.As<double>() / 1.8 : ToCelsius(value.As<double>());
        }
    }
}