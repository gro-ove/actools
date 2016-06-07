using System;
using System.Globalization;

namespace AcTools.Utils.Helpers {
    public static class InvariantParser {
        public static int? ToInvariantInt(this string s) {
            if (s == null) return null;

            int value;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ||
                    int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return value;
            }

            return null;
        }

        public static double? ToInvariantDouble(this string s) {
            if (s == null) return null;

            double value;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                return value;
            }

            return null;
        }
    }
}