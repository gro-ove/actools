using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using FirstFloor.ModernUI.Localizable;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public class PluralizingConverter : IValueConverter {
        public static string Pluralize(int value, [NotNull] string s, CultureInfo culture = null) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var result = Pluralizing.Convert(value, s);
            if (result == null || result == s) return s;
            if (result == string.Empty) return string.Empty;

            if (!s.Any(char.IsLower)) {
                return result.ToUpperInvariant();
            }

            if (char.IsUpper(s.FirstOrDefault())) {
                return result.Substring(0, 1).ToUpperInvariant() + result.Substring(1);
            }

            return result;
        }

        private static Regex _extRegex;
        private static Regex _lastRegex;

        public static string PluralizeExt(int value, [NotNull] string s, CultureInfo culture = null) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if (_extRegex == null) {
                _extRegex = new Regex(@"\{([^\d\s].*)\}", RegexOptions.Compiled);
                _lastRegex = new Regex(@"([^\d\s]+)\s*$", RegexOptions.Compiled);
            }

            var found = false;
            s = _extRegex.Replace(s, match => {
                found = true;
                return Pluralize(value, match.Groups[1].Value, culture);
            });

            if (!found) {
                s = _lastRegex.Replace(s, match => {
                    found = true;
                    return Pluralize(value, match.Groups[1].Value, culture);
                });

                if (!found) return Pluralize(value, s);
            }
            
            return s.Contains('{') ? string.Format(s, value) : s;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return parameter == null ? null : PluralizeExt(value.AsInt(), parameter.ToString(), culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
