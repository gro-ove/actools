using System;
using System.Linq;
using System.Windows.Data;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class PluralizingConverter : IValueConverter {
        public static string Pluralize([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if (s.EndsWith("y")) {
                return s.Substring(0, s.Length - 1) + "ies";
            }

            return s + "s";
        }

        public static string Pluralize(int value, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var t = Equals(value, 1) || value > 20 && Equals(value % 10, 1) ? s : Pluralize(s);
            return t.Contains('{') ? string.Format(t, value) : t;
        }

        public static string Pluralize(int value, [NotNull] string[] s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Length != 2) throw new ArgumentException(@"Argument is invalid collection", nameof(s));

            var t = Equals(value, 1) || value > 20 && Equals(value % 10, 1) ? s[0] : s[1];
            return t.Contains('{') ? string.Format(t, value) : t;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value == null || parameter == null) return null;
            int number;
            if (!int.TryParse(value.ToString(), out number)) {
                number = 0;
            }

            var str = parameter.ToString();
            return str.Contains('|') ? Pluralize(number, str.Split(new[] { '|' }, 2)) : Pluralize(number, str);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
