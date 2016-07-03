using System.Text.RegularExpressions;

namespace StringBasedFilter.Utils {
    public static class RegexFromQuery {
        private static readonly Regex ConvertationRegex;

        static RegexFromQuery() {
            ConvertationRegex = new Regex(@"(?=[\\\$^.+(){}[\]|])", RegexOptions.Compiled);
        }

        public static Regex Create(string query, bool wholeMatch, bool strictMode) {
            return new Regex((strictMode || wholeMatch ? @"^" : @"\b") +
                    ConvertationRegex.Replace(query, @"\").Replace("*", ".*").Replace("?", ".") +
                    (strictMode ? @"$" : ""),
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
    }
}
