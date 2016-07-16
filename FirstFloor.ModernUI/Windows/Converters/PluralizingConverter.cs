using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
using JetBrains.Annotations;

// Localize me!
namespace FirstFloor.ModernUI.Windows.Converters {
    internal static class PluralizingDictionary {
        [CanBeNull]
        public static string PluralizeEn(string s) {
            switch (s) {
                case "child": return "children";
                case "person": return "people";
                case "man": return "men";
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

        [CanBeNull]
        public static string PluralizeRu(string s, bool two) {
            switch (s) {
                case "круг": return two ? "круга" : "кругов";
                case "оппонент": return two ? "оппонента" : "оппонентов";
                case "противник": return two ? "противника" : "противников";
            }

            return null;
        }
    }
    
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
                        PluralizeRu(s, value > 1 && value < 5 || value > 20 && value % 10 < 5);
            } else {
                return Equals(value, 1) ? s : PluralizeEn(s);
            }
        }

        private static Regex _extRegex;

        public static string PluralizeExt(int value, [NotNull] string s, CultureInfo culture = null) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            if (_extRegex == null) {
                _extRegex = new Regex(@"\{([^\d\s].*)\}", RegexOptions.Compiled);
            }

            var found = false;
            s = _extRegex.Replace(s, match => {
                found = true;
                return Pluralize(value, match.Groups[1].Value, culture);
            });

            if (!found) {
                s = Pluralize(value, s, culture);
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
