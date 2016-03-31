using System.Globalization;
using System.Text.RegularExpressions;

namespace StringBasedFilter.Utils {
    public static class FlexibleParser {
        private static Regex _parseDouble;

        public static bool TryParseDouble(string s, out double value) {
            if (double.TryParse(s, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out value)) return true;

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
