using System.Globalization;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Defines rules: which form of what should be selected depending on number. Words’ forms
    /// themself are defined in PluralizingDictionary.
    /// </summary>
    internal static class Pluralizing {
        private static string En(int v, string s) {
            return v == 1 ? s : PluralizingDictionary.En(s);
        }

        private static string Ru(int v, string s) {
            if (s == string.Empty) return string.Empty;

            if (s[0] == '!') {
                return v == 1 ? s.Substring(1) : PluralizingDictionary.RuAlt(s.Substring(1));
            }

            var last = v % 10;
            if (last == 0 || last > 4 || v > 10 && v < 20) {
                return PluralizingDictionary.Ru(s, false);
            }

            return last == 1 ? s : PluralizingDictionary.Ru(s, true);
        }

        public static string Convert(int v, string s) {
            switch (CultureInfo.CurrentUICulture.Name.ToLowerInvariant()) {
                case "ru":
                case "ru-ru":
                    return Ru(v, s);
                default:
                    return En(v, s);
            }
        }
    }
}