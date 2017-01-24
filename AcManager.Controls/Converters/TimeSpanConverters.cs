using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(TimeSpan), typeof(string))]
    public class TimeSpanHhMmConverter : IValueConverter {
        private static string Convert(TimeSpan t) {
            if (t < TimeSpan.Zero) {
                return $@"-{Convert(-t)}";
            }
            return $@"{t.TotalHours.RoundToInt():D2}:{t.Minutes:D2}";
        }

        private static TimeSpan Convert(string s) {
            var p = s?.Split(new[] { ':' }, 3).Select(x => FlexibleParser.ParseDouble(x)).ToList();
            switch (p?.Count) {
                case 1:
                    return TimeSpan.FromMinutes((p[0] < 0 ? -1 : 1) * p[0].Abs());
                case 2:
                    return TimeSpan.FromMinutes((p[0] < 0 ? -1 : 1) * (p[0].Abs() * 60 + p[1]));
                default:
                    return default(TimeSpan);
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is TimeSpan ? Convert((TimeSpan)value) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Convert(value?.ToString());
        }
    }

    [ValueConversion(typeof(TimeSpan), typeof(string))]
    public class TimeSpanHhMmSsConverter : IValueConverter {
        private static string Convert(TimeSpan t) {
            if (t < TimeSpan.Zero) {
                return $@"-{Convert(-t)}";
            }
            return t.TotalHours > 0 ? $@"{t.TotalHours.RoundToInt():D2}:{t.Minutes:D2}:{t.Seconds:D2}" : $@"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private static TimeSpan Convert(string s) {
            var p = s?.Split(new[] { ':' }, 3).Select(x => FlexibleParser.ParseDouble(x)).ToList();
            switch (p?.Count) {
                case 1:
                    return TimeSpan.FromSeconds((p[0] < 0 ? -1 : 1) * p[0].Abs());
                case 2:
                    return TimeSpan.FromSeconds((p[0] < 0 ? -1 : 1) * (p[0].Abs() * 60 + p[1].Abs()));
                case 3:
                    return TimeSpan.FromSeconds((p[0] < 0 ? -1 : 1) * (p[0].Abs() * 3600 + p[1].Abs() * 60 + p[2].Abs()));
                default:
                    return default(TimeSpan);
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is TimeSpan ? Convert((TimeSpan)value) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Convert(value?.ToString());
        }
    }

    [ValueConversion(typeof(TimeSpan), typeof(double))]
    public class TimeSpanMinutesConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as TimeSpan?)?.TotalMinutes ?? 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return TimeSpan.FromMinutes(value.AsDouble().Round(parameter.AsDouble()));
        }
    }

    [ValueConversion(typeof(TimeSpan), typeof(double))]
    public class TimeSpanSecondsConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (value as TimeSpan?)?.TotalSeconds ?? 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return TimeSpan.FromSeconds(value.AsDouble().Round(parameter.AsDouble()));
        }
    }
}