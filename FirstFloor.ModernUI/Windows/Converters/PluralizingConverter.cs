using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using FirstFloor.ModernUI.Localizable;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    [ValueConversion(typeof(int), typeof(string))]
    public class PluralizingConverter : IValueConverter {
        public static string Pluralize(int value, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var result = Pluralizing.Convert(value, s.ToLower(CultureInfo.CurrentUICulture));
            if (result == null || result == s) return s;
            if (result == string.Empty) return string.Empty;

            if (!s.Any(char.IsLower)) {
                return result.ToUpper(CultureInfo.CurrentUICulture);
            }

            if (char.IsUpper(s[0]) || s[0] == '!' && s.Length > 1 && char.IsUpper(s[1])) {
                return result.Substring(0, 1).ToUpper(CultureInfo.CurrentUICulture) + result.Substring(1);
            }

            return result;
        }

        private static Regex _extRegex;
        private static Regex _lastRegex;

        public static string PluralizeExt(int value, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if (_extRegex == null) {
                _extRegex = new Regex(@"\{([^\d\s].*)\}", RegexOptions.Compiled);
                _lastRegex = new Regex(@"((?:[^\d\s]+ |[^\d\s]+)+)\s*$", RegexOptions.Compiled);
            }

            var found = false;
            s = _extRegex.Replace(s, match => {
                found = true;
                return Pluralize(value, match.Groups[1].Value);
            });

            if (!found) {
                s = _lastRegex.Replace(s, match => {
                    found = true;
                    return Pluralize(value, match.Groups[1].Value);
                });

                if (!found) return Pluralize(value, s);
            }

            return s.Contains('{') ? string.Format(s, value) : s;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter == null ? null : PluralizeExt(value.As<int>(), parameter.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
