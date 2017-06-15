using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class StringExtension {
        public static string RandomString(int length){
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[MathUtils.Random(s.Length)]).ToArray());
        }

        [Pure]
        public static int CompareAsVersionTo([CanBeNull] this string a, [CanBeNull] string b) {
            if (a == null) return b == null ? 0 : -1;
            if (b == null) return 1;

            var ap = a.Split('.');
            var bp = b.Split('.');

            for (var i = 0; i < ap.Length && i < bp.Length; i++) {
                var c = AlphanumComparatorFast.Compare(ap[i], bp[i]);
                if (c != 0) return c;
            }

            return ap.Length - bp.Length;
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
            if (apart == string.Empty) return s;
            return apart == null ? s : s.EndsWith(apart) ? s.ApartFromLast(apart.Length) : s;
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

        /// <summary>
        /// Convert bytes to using UTF8 (only if it’s a correct one) or Default encoding.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [Pure, NotNull]
        public static string ToUtf8String([NotNull] this byte[] bytes) {
            return (UTF8Checker.IsUtf8(bytes, 200) ? Encoding.UTF8 : Encoding.Default).GetString(bytes);
        }

        [Pure]
        public static bool DiapasonContains([NotNull] this string s, double value, bool roundSingle = true) {
            if (s == null) throw new ArgumentNullException(nameof(s));

            return s.Split(',', ';').Select(x => x.Trim()).Any(part => {
                var n = part.IndexOf('-');
                if (n == 0) {
                    var m = part.IndexOf('-', n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                double fromValue, toValue;
                if (n > 0 && n < part.Length - 1) { // "x-y"
                    if (FlexibleParser.TryParseDouble(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseDouble(part.Substring(n + 1), out toValue)) {
                        return value >= fromValue && value <= toValue;
                    }
                } else if (n < 0) { // "x"
                    if (FlexibleParser.TryParseDouble(part, out fromValue)) {
                        return roundSingle ? value.RoughlyEquals(fromValue) : Equals(fromValue, value);
                    }
                } else if (part.Length == 1) { // "-"
                    return true;
                } else if (n == part.Length - 1) { // "x-"
                    if (FlexibleParser.TryParseDouble(part.Substring(0, n), out fromValue)) {
                        return value >= fromValue;
                    }
                } else { // "-x"
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
                int n = part.IndexOf('-'), fromValue, toValue;
                if (n == 0) {
                    var m = part.IndexOf('-', n + 1);
                    if (m != -1 && m != 1) {
                        n = m;
                    }
                }

                if (n > 0 && n < part.Length - 1) { // "x-y"
                    if (FlexibleParser.TryParseTime(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseTime(part.Substring(n + 1), out toValue)) {
                        return value >= fromValue && value <= toValue;
                    }
                } else if (n < 0) { // "x"
                    if (FlexibleParser.TryParseTime(part, out fromValue)) {
                        if (roundSingle) {
                            var delimiters = part.Count(':');
                            return fromValue.Equals(delimiters == 2 ? value : delimiters == 1 ? value.Floor(60) : value.Floor(60 * 60));
                        }

                        return Equals(fromValue, value);
                    }
                } else if (part.Length == 1) { // "-"
                    return true;
                } else if (n == part.Length - 1) { // "x-"
                    if (FlexibleParser.TryParseTime(part.Substring(0, n), out fromValue)) {
                        return value >= fromValue;
                    }
                } else { // "-x"
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

                if (n > 0 && n < part.Length - 1) { // "x-y"
                    if (FlexibleParser.TryParseInt(part.Substring(0, n), out fromValue) &&
                            FlexibleParser.TryParseInt(part.Substring(n + 1), out toValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + Math.Min(toValue, max) - fromValue);
                    }
                } else if (n < 0) { // "x"
                    if (FlexibleParser.TryParseInt(part, out fromValue) && fromValue >= min && fromValue <= max) {
                        return new[] { fromValue };
                    }
                } else if (part.Length == 1) { // "-"
                    return Enumerable.Range(min, 1 + max - min);
                } else if (n == part.Length - 1) { // "x-"
                    if (FlexibleParser.TryParseInt(part.Substring(0, n), out fromValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + max - fromValue);
                    }
                } else { // "-x"
                    if (FlexibleParser.TryParseInt(part.Substring(n + 1), out toValue)) {
                        return Enumerable.Range(min, 1 + Math.Min(toValue, max) - min);
                    }
                }

                return new int[0];
            });
        }
    }
}
