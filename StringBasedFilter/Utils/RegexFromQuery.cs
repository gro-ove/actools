using System.Text.RegularExpressions;

namespace StringBasedFilter.Utils {
    public static class RegexFromQuery {
        private static readonly Regex ConvertationRegex;

        static RegexFromQuery() {
            ConvertationRegex = new Regex(@"(?=[\\\$^.+(){}[\]|])", RegexOptions.Compiled);
        }

        public static Regex Create(string query, bool wholeMatch) {
            return new Regex((wholeMatch ? @"^" : @"\b") + ConvertationRegex.Replace(query, @"\").Replace("*", ".*").Replace("?", "."),
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
    }
}
