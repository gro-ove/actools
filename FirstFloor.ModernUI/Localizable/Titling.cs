using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FirstFloor.ModernUI.Localizable {
    /// <summary>
    /// Converts normal string to a title case.
    /// </summary>
    [Localizable(false)]
    internal static class Titling {
        private static string CapitalizeFirst(string s, CultureInfo culture) {
            if (s == string.Empty) return string.Empty;
            if (s.Length == 1) return s.ToUpper(culture);
            return char.ToUpper(s[0], culture) + s.Substring(1);
        }

        #region English
        private static readonly Regex EnTitleCaseRegex =
                new Regex(@"^[a-z]|\b(?!(?:a|an|and|as|at|but|by|en|for|if|in|of|on|or|the|to|v[.]?|via|vs)\b)[a-z]\w+|\b[a-z]\w*$",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string En(string s, CultureInfo culture) {
            return EnTitleCaseRegex.Replace(s, m => CapitalizeFirst(m.Value, culture));
        }
        #endregion

        #region French
        private static readonly Regex FrWordsRegex = new Regex(@"[a-zA-Z0-9àâäèéêëîïôœùûüÿçÀÂÄÈÉÊËÎÏÔŒÙÛÜŸÇ]+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] FrLowercaseOnly = {
            "le", "la", "les",
            // indefinite articles
            "un", "une", "des",
            // partitive articles
            "du", "de", "des",
            // contracted articles
            "au", "aux", "du", "des",
            // demonstrative adjectives
            "ce", "cet", "cette", "ces",
            // exclamative adjectives
            "quel", "quels", "quelle", "quelles",
            // possessive adjectives
            "mon", "ton", "son", "notre", "votre", "leur", "ma", "ta", "sa", "mes", "tes", "ses", "nos", "vos", "leurs",
            // coordinating conjunctions
            "mais", "ou", "et", "donc", "or", "ni", "car", "voire",
            // subordinating conjunctions
            "que", "qu", "quand", "comme", "si", "lorsque", "lorsqu", "puisque", "puisqu", "quoique", "quoiqu",
            // prepositions
            "à", "chez", "dans", "entre", "jusque", "jusqu", "hors", "par", "pour", "sans", "vers", "sur", "pas", "parmi", "avec", "sous", "en",
            // personal pronouns
            "je", "tu", "il", "elle", "on", "nous", "vous", "ils", "elles", "me", "te", "se", "y",
            // relative pronouns
            "qui", "que", "quoi", "dont", "où",
            // others
            "ne"
        };

        private static readonly string[] FrCapitalizeAfterQuoteAnd = {
            "l", "d"
        };

        private static readonly string[] FrLowerCaseAfterQuoteAnd = {
            "c", "j", "m", "n", "s", "t"
        };

        private static string FrCapitalizeFirstIfNeeded(string s, CultureInfo culture) {
            return FrLowercaseOnly.Contains(s) ? s : CapitalizeFirst(s, culture);
        }

        private static string FrCapitalizeWithQuote(string s, CultureInfo culture) {
            var p = s.Split(new [] { '\'' }, 2);
            if (p.Length == 2) {
                // could be d' or l', if it is the first word (l'Autre)
                if (FrCapitalizeAfterQuoteAnd.Contains(p[0])) {
                    return p[0] + '\'' + FrCapitalizeFirstIfNeeded(p[1], culture);
                }

                // could be c', m', t', j', n', s' if it is the first word (c'est)
                if (FrLowerCaseAfterQuoteAnd.Contains(p[0])) {
                    return s;
                }

                // could be 's
                if (p[1].Length == 1) {
                    return FrCapitalizeFirstIfNeeded(p[0], culture) + '\'' + p[1];
                }

                // could be jusqu'au
                return FrCapitalizeFirstIfNeeded(p[0], culture) + '\'' + FrCapitalizeFirstIfNeeded(p[1], culture);
            }

            return s;
        }

        private static string Fr(string s, CultureInfo culture) {
            // algorithm from https://github.com/benoitvallon/titlecase-french/blob/master/index.js
            var i = 0;
            return FrWordsRegex.Replace(s, m => {
                ++i;
                var w = m.Value.ToLower(culture);
                
                if (w.Contains('-')) {
                    var p = w.Split(new[] { '-' }, 2);
                    return CapitalizeFirst(p[0], culture) + '-' + FrCapitalizeFirstIfNeeded(p[1], culture);
                }
                
                var isComposedWord = w.Contains('\'');
                if (isComposedWord) {
                    w = FrCapitalizeWithQuote(w, culture);
                }

                if (w.Contains('.')) {
                    return string.Join(".", w.Split('.').Select(y => CapitalizeFirst(y, culture)));
                }

                if (i == 1) {
                    return CapitalizeFirst(w, culture);
                }

                return isComposedWord ? w : FrCapitalizeFirstIfNeeded(w, culture);
            });
        }
        #endregion

        #region Polish
        private static string Pl(string s, CultureInfo culture) {
            return CapitalizeFirst(s, culture);
        }
        #endregion

        #region Russian
        private static string Ru(string s, CultureInfo culture) {
            return CapitalizeFirst(s, culture);
        }
        #endregion

        #region Spanish
        private static string Es(string s, CultureInfo culture) {
            return CapitalizeFirst(s, culture);
        }
        #endregion

        #region Portuguese
        private static readonly Regex PtTitleCaseRegex =
                new Regex(@"^[a-z]|\b(?!(?:da|de|do|das|des|dos)\b)[a-z]\w+|\b[a-z]\w*$",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Pt(string s, CultureInfo culture) {
            return PtTitleCaseRegex.Replace(s, m => CapitalizeFirst(m.Value, culture));
        }
        #endregion

        public static string Convert(string s, CultureInfo culture) {
            if (culture.Name.Length < 2) return En(s, culture);
            switch (culture.Name.Substring(0, 2).ToLowerInvariant()) {
                case "en":
                    return En(s, culture);
                case "es":
                    return Es(s, culture);
                case "fr":
                    return Fr(s, culture);
                case "pl":
                    return Pl(s, culture);
                case "pt":
                    return Pt(s, culture);
                case "ru":
                    return Ru(s, culture);
                default:
                    return CapitalizeFirst(s, culture);
            }
        }

        public static string Convert(string s) {
            return Convert(s, CultureInfo.CurrentUICulture);
        }
    }
}