using System;
using System.Globalization;
using System.Windows.Data;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(TimeSpan), typeof(string))]
    public class MillisecondsTimeSpanConverter : IValueConverter {
        private static string ToMillisecondsString(TimeSpan span) {
            return span.TotalHours > 1d
                    ? $@"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds:D3}"
                    : $@"{span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds:D3}";
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is TimeSpan) {
                return ToMillisecondsString((TimeSpan)value);
            } else {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}