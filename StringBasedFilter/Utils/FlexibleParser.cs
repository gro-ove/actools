using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StringBasedFilter.Utils {
    internal static class FlexibleParser {
        private static Regex _parseInt, _parseDouble;

        public static bool TryParseInt(string s, out int value) {
            if (s == null) {
                value = 0;
                return false;
            }

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ||
                    int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseInt == null) {
                _parseInt = new Regex(@"-? *\d+", RegexOptions.Compiled);
            }

            var match = _parseInt.Match(s);
            if (match.Success) {
                return int.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out value);
            }

            value = 0;
            return false;
        }

        public static bool TryParseDouble(string s, out double value) {
            if (s == null) {
                value = 0d;
                return false;
            }

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseDouble == null) {
                _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?", RegexOptions.Compiled);
            }
        
            var match = _parseDouble.Match(s);
            if (match.Success) {
                return double.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out value);
            }

            value = 0.0;
            return false;
        }

        public static double? TryParseDouble(string s) {
            double d;
            return TryParseDouble(s, out d) ? d : 0d;
        }
    }
}
