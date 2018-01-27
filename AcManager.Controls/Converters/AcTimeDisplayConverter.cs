using System;
using System.Globalization;
using System.Windows.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Serialization;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(int), typeof(string))]
    public class AcTimeDisplayConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var time = value.As<int>();
            return $@"{time / 60 / 60:D2}:{time / 60 % 60:D2}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (FlexibleParser.TryParseTime(value?.ToString(), out var time)) return time;
            throw new FormatException("Can’t parse time");
        }
    }
}