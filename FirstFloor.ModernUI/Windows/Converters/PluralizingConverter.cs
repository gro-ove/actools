using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

// Localize me!
namespace FirstFloor.ModernUI.Windows.Converters {
    public class PluralizingConverter : IValueConverter {
        public static string PluralizeEn([NotNull] string input) {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var result = PluralizingDictionary.PluralizeEn(input.ToLowerInvariant());
            if (result == null) return input + "(s)";
            if (result == string.Empty) return string.Empty;

            if (!input.Any(char.IsLower)) {
                return result.ToUpperInvariant();
            }

            if (char.IsUpper(input.FirstOrDefault())) {
                return result.Substring(0, 1).ToUpperInvariant() + result.Substring(1);
            }

            return result;
        }

        public static string PluralizeRu([NotNull] string input, bool two) {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var result = PluralizingDictionary.PluralizeRu(input.ToLowerInvariant(), two);
            if (result == null) return input + "(и)";
            if (result == string.Empty) return string.Empty;

            if (!input.Any(char.IsLower)) {
                return result.ToUpperInvariant();
            }

            if (char.IsUpper(input.FirstOrDefault())) {
                return result.Substring(0, 1).ToUpperInvariant() + result.Substring(1);
            }

            return result;
        }

        public static string Pluralize(int value, [NotNull] string s, CultureInfo culture = null) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if ((culture ?? CultureInfo.CurrentUICulture).Name == "ru-RU") {
                return value == 1 || value > 20 && value % 10 == 1 ? s :
                        PluralizeRu(s, value > 1 && value < 5 || value > 20 && value % 10 > 1 && value % 10 < 5);
            } else {
                return Equals(value, 1) ? s : PluralizeEn(s);
            }
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
