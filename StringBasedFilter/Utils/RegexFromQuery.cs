using System.Text;
using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter.Utils {
    public static class RegexFromQuery {
        public static bool IsQuerySymbol(char c) {
            return c == '*' || c == '?';
        }

        public static bool IsQuery(string s) {
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];

                if (c == '\\') {
                    i++;
                    continue;
                }

                if (IsQuerySymbol(c)) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRegexMetachar(char c) {
            return c >= 9 && c <= 10 || c >= 12 && c <= 13 || c == 32 || c >= 35 && c <= 36 || c >= 40 && c <= 43 || c == 46 || c == 63 || c >= 91 && c <= 92 ||
                    c == 94 || c >= 123 && c <= 124;
        }

        private static char ConvertBack(char c) {
            switch (c) {
                case '\t':
                    return 't';
                case '\n':
                    return 'n';
                case '\f':
                    return 'f';
                case '\r':
                    return 'r';
                default:
                    return c;
            }
        }

        private static void AppendEscaped(char c, StringBuilder builder) {
            if (IsRegexMetachar(c)) {
                builder.Append('\\');
                builder.Append(ConvertBack(c));
            } else {
                builder.Append(c);
            }
        }

        private static string PrepareQuery(string query, StringMatchMode mode) {
            var result = new StringBuilder((int)(query.Length * 1.2 + 2));
            result.Append(mode != StringMatchMode.IncludedWithin ? @"^" : @"\b");

            for (var i = 0; i < query.Length; i++) {
                var c = query[i];

                if (c == '\\') {
                    var n = i + 1 < query.Length ? query[i + 1] : (char)0;
                    if (IsQuerySymbol(n)) {
                        i++;
                        AppendEscaped(n, result);
                        continue;
                    }
                }

                if (c == '*') {
                    result.Append(".*");
                } else if (c == '?') {
                    result.Append(".");
                } else {
                    AppendEscaped(query[i], result);
                }
            }

            if (mode == StringMatchMode.CompleteMatch) {
                result.Append(@"$");
            }

            return result.ToString();
        }

        public static Regex Create(string query, StringMatchMode mode) {
            return new Regex(PrepareQuery(query, mode), RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public static Regex Create(string query, StringMatchMode mode, bool ignoreCase) {
            return new Regex(PrepareQuery(query, mode), ignoreCase ? RegexOptions.Compiled | RegexOptions.IgnoreCase : RegexOptions.Compiled);
        }
    }
}
