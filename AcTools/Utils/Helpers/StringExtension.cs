using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class StringExtension {
        public static int ComputeLevenshteinDistance([CanBeNull] this string s, [CanBeNull] string t) {
            if (string.IsNullOrEmpty(s)) {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t)) {
                return s.Length;
            }

            var n = s.Length;
            var m = t.Length;
            var d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (var i = 0; i <= n; d[i, 0] = i++) { }
            for (var j = 1; j <= m; d[0, j] = j++) { }
            for (var i = 1; i <= n; i++) {
                for (var j = 1; j <= m; j++) {
                    var cost = t[j - 1] == s[i - 1] ? 0 : 1;
                    var min1 = d[i - 1, j] + 1;
                    var min2 = d[i, j - 1] + 1;
                    var min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }

            return d[n, m];
        }

        public static int Count([NotNull] this string s, params char[] c) {
            var r = 0;
            for (var i = 0; i < s.Length; i++) {
                if (c.ArrayContains(s[i])) r++;
            }
            return r;
        }

        public static int CountLines([NotNull] this string s) {
            var r = 1;
            for (var i = 0; i < s.Length; i++) {
                switch (s[i]) {
                    case '\n':
                        r++;
                        break;
                    case '\r':
                        r++;
                        if (i < s.Length - 1 && s[i + 1] == '\n') {
                            i++;
                        }
                        break;
                }
            }
            return r;
        }

        public static string[] ToLines([NotNull] this string s, bool trimSpaces = true, bool skipEmpty = true) {
            var result = new string[s.CountLines()];

            int i = 0, j = 0, l = 0, p = 0;
            var n = false;
            for (; i < s.Length; i++) {
                var c = s[i];
                switch (c) {
                    case '\r':
                        End();
                        if (i < s.Length - 1 && s[i + 1] == '\n') {
                            j = ++i + 1;
                        }
                        break;
                    case '\n':
                        End();
                        break;
                    default:
                        if (!trimSpaces || !char.IsWhiteSpace(c)) {
                            l = i + 1;
                            n = true;
                        } else if (!n) {
                            j = l = i + 1;
                        }
                        break;
                }
            }

            End();
            if (p < result.Length){
                Array.Resize(ref result, p);
            }

            return result.ToArray();

            void End(){
                if (!skipEmpty || (j < s.Length && l > j)){
                    if (result.Length <= p){
                        Array.Resize(ref result, p + 1);
                    }
                    result[p++] = j >= s.Length ? string.Empty : s.Substring(j, l - j);
                }
                j = l = i + 1;
                n = false;
            }
        }

        public static string RandomString(int length) {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[MathUtils.Random(s.Length)]).ToArray());
        }

        public static string GetChecksum([NotNull] this string s) {
            using (var sha1 = new SHA1Managed()) {
                return sha1.ComputeHash(Encoding.UTF8.GetBytes(s)).ToHexString();
            }
        }

        [Pure]
        public static int CompareAsVersionTo([CanBeNull] this string a, [CanBeNull] string b) {
            if (a == null) return b == null ? 0 : -1;
            if (b == null) return 1;

            var ap = a.Trim().ApartFromFirst("v").Split('.');
            var bp = b.Trim().ApartFromFirst("v").Split('.');

            for (var i = 0; i < ap.Length && i < bp.Length; i++) {
                var c = AlphanumComparatorFast.Compare(ap[i].Trim(), bp[i].Trim());
                if (c != 0) return c;
            }

            return ap.Length - bp.Length;
        }

        [CanBeNull, ContractAnnotation("b:notnull=>notnull"), Pure]
        public static string Or([CanBeNull] this string a, string b, params string[] c) {
            return !string.IsNullOrWhiteSpace(a) ? a : !string.IsNullOrWhiteSpace(b) ? b
                    : c.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

        [Pure]
        public static string ToBase64([NotNull] this string a) {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(a));
        }

        [Pure]
        public static string UriEscape([CanBeNull] this string a, bool plusForSpace = false) {
            return a == null ? null : plusForSpace ? Uri.EscapeDataString(a).Replace(@"%20", @"+") : Uri.EscapeDataString(a);
        }

        [Pure]
        public static bool IsVersionNewerThan([CanBeNull] this string currentVersion, [CanBeNull] string checkableVersion) {
            return currentVersion.CompareAsVersionTo(checkableVersion) > 0;
        }

        [Pure, ContractAnnotation("s:null=>null")]
        public static string RepeatString([CanBeNull] this string s, int number) {
            if (s == null) return null;
            switch (number) {
                case 0:
                    return string.Empty;
                case 1:
                    return s;
                case 2:
                    return s + s;
                default:
                    var b = new StringBuilder();
                    for (var i = 0; i < number; i++) {
                        b.Append(s);
                    }
                    return b.ToString();
            }
        }

        /// <summary>
        /// Word wraps the given text to fit within the specified width. From
        /// https://www.codeproject.com/Articles/51488/Implementing-Word-Wrap-in-C.
        /// </summary>
        /// <param name="text">Text to be word wrapped</param>
        /// <param name="width">Width, in characters, to which the text
        /// should be word wrapped</param>
        /// <returns>The modified text</returns>
        public static string WordWrap(this string text, int width) {
            int pos, next;
            var sb = new StringBuilder();

            // Lucidity check
            if (width < 1) return text;

            // Parse each line of text
            for (pos = 0; pos < text.Length; pos = next) {
                // Find end of line
                var eol = text.IndexOf('\n', pos);
                if (eol == -1) next = eol = text.Length;
                else next = eol + 1;

                // Copy this line of text, breaking into smaller lines as needed
                if (eol > pos) {
                    do {
                        var len = eol - pos;
                        if (len > width) len = BreakLine(text, pos, width);
                        sb.Append(text, pos, len);
                        sb.Append('\n');

                        // Trim whitespace following break
                        pos += len;
                        while (pos < eol && char.IsWhiteSpace(text[pos])) pos++;
                    } while (eol > pos);
                } else {
                    sb.Append('\n'); // Empty line
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Locates position to break the given line so as to avoid
        /// breaking words.
        /// </summary>
        /// <param name="text">String that contains line of text</param>
        /// <param name="pos">Index where line of text starts</param>
        /// <param name="max">Maximum line length</param>
        /// <returns>The modified line length</returns>
        private static int BreakLine(string text, int pos, int max) {
            var i = max;
            while (i >= 0 && !char.IsWhiteSpace(text[pos + i])) {
                i--;
            }

            if (i < 0) return max;
            while (i >= 0 && char.IsWhiteSpace(text[pos + i])) {
                i--;
            }

            return i + 1;
        }

        [Pure]
        public static bool IsVersionOlderThan([CanBeNull] this string currentVersion, [CanBeNull] string checkableVersion) {
            return currentVersion.CompareAsVersionTo(checkableVersion) < 0;
        }

        [Pure, NotNull]
        public static string ForceReplace([NotNull] this string s, [NotNull] string oldValue, [NotNull] string newValue) {
            var index = s.IndexOf(oldValue, StringComparison.CurrentCulture);
            if (index == -1) throw new Exception("Old value not found");
            return s.Substring(0, index) + newValue + s.Substring(index + oldValue.Length);
        }

        [Pure, NotNull]
        public static string ForceReplace([NotNull] this string s, [NotNull] string oldValue, [NotNull] string newValue, StringComparison comparison) {
            var index = s.IndexOf(oldValue, comparison);
            if (index == -1) throw new Exception("Old value not found");
            return s.Substring(0, index) + newValue + s.Substring(index + oldValue.Length);
        }

        [Pure, NotNull]
        public static string Replace([NotNull] this string s, [NotNull] string oldValue, [NotNull] string newValue, StringComparison comparison) {
            var index = s.IndexOf(oldValue, comparison);
            return index != -1 ? s.Substring(0, index) + newValue + s.Substring(index + oldValue.Length) : s;
        }

        public static bool Contains(this string s, string sub, StringComparison comparison) {
            return s.IndexOf(sub, comparison) != -1;
        }

        [Pure, NotNull]
        public static string SubstringExt([NotNull] this string s, int from) {
            if (from < 0) {
                from = s.Length - from;
            }

            if (from > s.Length) {
                return string.Empty;
            }

            return from <= 0 ? s : s.Substring(from);
        }

        [ContractAnnotation(@"value: null => null; value: notnull => notnull")]
        public static string GetDomainNameFromUrl(this string value) {
            return value == null ? null : Regex.Replace(value, @"^(?:(?:https?)?://)?(?:www\.)?|(?<=\w)/.+$", "", RegexOptions.IgnoreCase);
        }

        [Pure, NotNull]
        public static string ApartFromLast([NotNull] this string s, int apart) {
            if (apart < 0) {
                apart = s.Length - apart;
            }

            if (apart > s.Length) {
                return string.Empty;
            }

            return apart <= 0 ? s : s.Substring(0, s.Length - apart);
        }

        [Pure, NotNull]
        public static string ApartFromFirst([NotNull] this string s, [CanBeNull] string apart) {
            if (apart == string.Empty) return s;
            return apart == null ? s : s.StartsWith(apart) ? s.Substring(apart.Length) : s;
        }

        [Pure, NotNull]
        public static string ApartFromFirst([NotNull] this string s, [CanBeNull] string apart, StringComparison comparisonType) {
            if (apart == string.Empty) return s;
            return apart == null ? s : s.StartsWith(apart, comparisonType) ? s.Substring(apart.Length) : s;
        }

        [Pure, NotNull]
        public static string ApartFromLast([NotNull] this string s, [CanBeNull] string apart) {
            return string.IsNullOrEmpty(apart) ? s : s.EndsWith(apart) ? s.ApartFromLast(apart.Length) : s;
        }

        public static string ApartFromLast([NotNull] this string s, [CanBeNull] string apart, StringComparison comparisonType) {
            if (apart == string.Empty) return s;
            return apart == null ? s : s.EndsWith(apart, comparisonType) ? s.ApartFromLast(apart.Length) : s;
        }

        [Pure, NotNull]
        public static string ReplaceLastOccurrence([NotNull] this string s, [NotNull] string what, [NotNull] string replacement) {
            var place = s.LastIndexOf(what, StringComparison.Ordinal);
            return place == -1 ? s : s.Remove(place, what.Length).Insert(place, replacement);
        }

        [Pure, NotNull]
        public static Encoding GetEncoding([NotNull] this byte[] bytes) {
            if (bytes.StartsWith(Encoding.UTF8.GetPreamble()) || Utf8Checker.IsUtf8(bytes, 200)) {
                return Encoding.UTF8;
            }

            if (bytes.StartsWith(Encoding.Unicode.GetPreamble())) return Encoding.Unicode;
            if (bytes.StartsWith(Encoding.BigEndianUnicode.GetPreamble())) return Encoding.BigEndianUnicode;
            if (bytes.StartsWith(Encoding.UTF32.GetPreamble())) return Encoding.UTF32;
            if (bytes.StartsWith(Encoding.UTF7.GetPreamble())) return Encoding.UTF7;
            return Encoding.Default;
        }

        /// <summary>
        /// Convert bytes to using UTF8 (only if it’s a correct one) or Default encoding.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [Pure, NotNull]
        public static string ToUtf8String([NotNull] this byte[] bytes) {
            return GetEncoding(bytes).GetString(bytes);
        }

        [Pure]
        public static bool DiapasonContains([NotNull] this string s, double value, bool roundSingle = true) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            return s.Split(',', ';').Select(x => x.Trim()).Any(part => {
                var n = part.IndexOfAny(new[] { '-', '…', '—', '–' });
                if (n == 0) {
                    var m = part.IndexOfAny(new[] { '-', '…', '—', '–' }, n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                double fromValue, toValue;
                if (n > 0 && n < part.Length - 1) {
                    // "x-y"
                    if (FlexibleParser.TryParseDouble(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseDouble(part.Substring(n + 1), out toValue)) {
                        return value >= fromValue && value <= toValue;
                    }
                } else if (n < 0) {
                    // "x"
                    if (FlexibleParser.TryParseDouble(part, out fromValue)) {
                        return roundSingle ? value.RoughlyEquals(fromValue) : Equals(fromValue, value);
                    }
                } else if (part.Length == 1) {
                    // "-"
                    return true;
                } else if (n == part.Length - 1) {
                    // "x-"
                    if (FlexibleParser.TryParseDouble(part.Substring(0, n), out fromValue)) {
                        return value >= fromValue;
                    }
                } else {
                    // "-x"
                    if (FlexibleParser.TryParseDouble(part.Substring(n + 1), out toValue)) {
                        return value <= toValue;
                    }
                }

                return false;
            });
        }

        [Pure]
        public static bool TimeDiapasonContains([NotNull] this string s, int value, bool roundSingle = true) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            return s.Split(',', ';').Select(x => x.Trim()).Any(part => {
                int n = part.IndexOfAny(new[] { '-', '…', '—', '–' }), fromValue, toValue;
                if (n == 0) {
                    var m = part.IndexOfAny(new[] { '-', '…', '—', '–' }, n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                if (n > 0 && n < part.Length - 1) {
                    // "x-y"
                    if (FlexibleParser.TryParseTime(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseTime(part.Substring(n + 1), out toValue)) {
                        return value >= fromValue && value <= toValue;
                    }
                } else if (n < 0) {
                    // "x"
                    if (FlexibleParser.TryParseTime(part, out fromValue)) {
                        if (roundSingle) {
                            var delimiters = part.Count(':');
                            return fromValue.Equals(delimiters == 2 ? value : delimiters == 1 ? value.Floor(60) : value.Floor(60 * 60));
                        }

                        return Equals(fromValue, value);
                    }
                } else if (part.Length == 1) {
                    // "-"
                    return true;
                } else if (n == part.Length - 1) {
                    // "x-"
                    if (FlexibleParser.TryParseTime(part.Substring(0, n), out fromValue)) {
                        return value >= fromValue;
                    }
                } else {
                    // "-x"
                    if (FlexibleParser.TryParseTime(part.Substring(n + 1), out toValue)) {
                        return value <= toValue;
                    }
                }

                return false;
            });
        }

        /// <summary>
        /// Only in about two times slower than Enumerable.Range.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        [Pure]
        public static IEnumerable<int> ToDiapason([NotNull] this string s, int min, int max) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max));

            return s.Split(',', ';').Select(x => x.Trim()).SelectMany(part => {
                int n = part.IndexOf('-'), fromValue, toValue;
                if (n == 0) {
                    var m = part.IndexOf('-', n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                if (n > 0 && n < part.Length - 1) {
                    // "x-y"
                    if (FlexibleParser.TryParseInt(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseInt(part.Substring(n + 1), out toValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + Math.Min(toValue, max) - fromValue);
                    }
                } else if (n < 0) {
                    // "x"
                    if (FlexibleParser.TryParseInt(part, out fromValue) && fromValue >= min && fromValue <= max) {
                        return new[] { fromValue };
                    }
                } else if (part.Length == 1) {
                    // "-"
                    return Enumerable.Range(min, 1 + max - min);
                } else if (n == part.Length - 1) {
                    // "x-"
                    if (FlexibleParser.TryParseInt(part.Substring(0, n), out fromValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + max - fromValue);
                    }
                } else {
                    // "-x"
                    if (FlexibleParser.TryParseInt(part.Substring(n + 1), out toValue)) {
                        return Enumerable.Range(min, 1 + Math.Min(toValue, max) - min);
                    }
                }

                return new int[0];
            });
        }

        private static readonly char[] Quotes = { '"', '\'', '`', '“', '”' };

        /// <summary>
        /// Turns string like “qwe 'ABC\' DEF' `r r`t 'ABC\' DEF' y” to “qwe ___sub_0__ ___sub_1__t ___sub_2__ y”,
        /// which later can be turned to “qwe ABC' DEF r rt ABC' DEF y” with unwrap callback.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <param name="unwrap">Callback for unwrapping.</param>
        /// <returns>Wrapped string.</returns>
        /// <remarks>I got slightly carried away trying to shorten it, sorry about it.</remarks>
        public static string WrapQuoted([CanBeNull] this string s, out Func<string, string> unwrap) {
            if (!(s?.IndexOfAny(Quotes) > -1)) {
                unwrap = x => x;
                return s;
            }

            var dictionary = new Dictionary<string, string>();
            StringBuilder r = new StringBuilder(s.Length), u = new StringBuilder();
            for (int i = 0, w = 0; i <= s.Length; i++) {
                string c = (i < s.Length ? s[i] : (char)w).ToString(), t;
                if (c == "\\" && i < s.Length - 1) {
                    (w > 0 ? u : r).Append((t = s[++i].ToString()) != "\\" && Array.IndexOf(Quotes, t[0]) == -1 ? c + t : t);
                } else if (c[0] == w && w > 0) {
                    r.Append(t = "___sub_" + dictionary.Count + "__");
                    dictionary[t] = u.ToString();
                    w = u.Clear().Length;
                } else if (w > 0) {
                    u.Append(c);
                } else if (Array.IndexOf(Quotes, c[0]) != -1) {
                    w = c == "“" ? '”' : c[0];
                } else if (c[0] > 0) {
                    r.Append(c);
                }
            }
            unwrap = x => dictionary.Aggregate(x, (t, p) => t.Replace(p.Key, p.Value));
            return r.ToString();
        }

        #region Cut Base64
        [Pure, CanBeNull]
        public static string ToCutBase64([CanBeNull] this string decoded) {
            return decoded == null ? null : Encoding.UTF8.GetBytes(decoded).ToCutBase64();
        }
        #endregion
    }
}