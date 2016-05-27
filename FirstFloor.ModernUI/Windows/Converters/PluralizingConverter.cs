using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    internal static class PluralizingDictionary {
        [CanBeNull]
        public static string Pluralize(string s) {
            switch (s) {
                case "child":
                    return "children";

                case "person":
                    return "people";

                case "man":
                    return "men";
            }

            if (s.EndsWith("o")) {
                return null;
            }

            if (s.EndsWith("s") || s.EndsWith("x") || s.EndsWith("ch") || s.EndsWith("sh")) {
                return s + "es";
            }

            if (s.EndsWith("y")) {
                return s.Substring(0, s.Length - 1) + "ies";
            }

            return s + "s";
        }

        internal static bool IsLowerCase(this char c) {
            return c >= 'a' && c <= 'z';
        }

        internal static bool IsUpperCase(this char c) {
            return c >= 'A' && c <= 'Z';
        }
    }

    public class PluralizingConverter : IValueConverter {
        public static string Pluralize([NotNull] string input) {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var result = PluralizingDictionary.Pluralize(input.ToLowerInvariant());
            if (result == null) return "?";
            if (result == string.Empty) return string.Empty;

            if (!input.Any(x => x.IsLowerCase())) {
                return result.ToUpperInvariant();
            }

            if (input.FirstOrDefault().IsUpperCase()) {
                return result.Substring(0, 1).ToUpperInvariant() + result.Substring(1);
            }

            return result;
        }

        public static string Pluralize(int value, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return Equals(value, 1) || value > 20 && Equals(value % 10, 1) ? s : Pluralize(s);
        }

        private static Regex _extRegex;

        public static string PluralizeExt(int value, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if (_extRegex == null) {
                _extRegex = new Regex(@"\{([a-zA-Z].*)\}", RegexOptions.Compiled);
            }

            var found = false;
            s = _extRegex.Replace(s, match => {
                found = true;
                return Pluralize(value, match.Groups[1].Value);
            });

            if (!found) {
                s = Pluralize(value, s);
            }
            
            return s.Contains('{') ? string.Format(s, value) : s;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            int number;
            return value == null || parameter == null ? null :
                    PluralizeExt(int.TryParse(value.ToString(), out number) ? number : 0, parameter.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
