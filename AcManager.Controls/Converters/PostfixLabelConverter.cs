using System;
using System.Globalization;
using System.Windows.Data;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(int), typeof(string))]
    public class PostfixLabelConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string s && s.Length > 0 && char.IsLetter(s[0]) ? "" : parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value;
        }
    }
}