using System.Globalization;
using System.Text.RegularExpressions;

namespace StringBasedFilter.Utils {
    public static class FlexibleParser {
        private static Regex _parseInt, _parseDouble;

        public static bool TryParseInt(string s, out int value) {
            if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseInt == null) {
                _parseInt = new Regex(@"-? *\d+", RegexOptions.Compiled);
            }

            if (s != null) {
                var match = _parseDouble.Match(s);
                if (match.Success) {
                    return int.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out value);
                }
            }

            value = 0;
            return false;
        }

        public static bool TryParseDouble(string s, out double value) {
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return true;
            }

            if (_parseDouble == null) {
                _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?", RegexOptions.Compiled);
            }

            if (s != null) {
                var match = _parseDouble.Match(s);
                if (match.Success) {
                    return double.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out value);
                }
            }

            value = 0.0;
            return false;
        }
    }
}
