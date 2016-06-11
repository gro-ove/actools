using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    internal class AlphanumComparatorFast : IComparer {
        int IComparer.Compare(object x, object y) {
            return Compare(x, y);
        }

        public static int Compare(string x, string y) {
            var len1 = x.Length;
            var len2 = y.Length;
            var marker1 = 0;
            var marker2 = 0;

            // Walk through two the strings with two markers.
            while (marker1 < len1 && marker2 < len2) {
                var ch1 = x[marker1];
                var ch2 = y[marker2];

                // Some buffers we can build up characters in for each chunk.
                var space1 = new char[len1];
                var loc1 = 0;
                var space2 = new char[len2];
                var loc2 = 0;

                // Walk through all following characters that are digits or
                // characters in BOTH strings starting at the appropriate marker.
                // Collect char arrays.
                do {
                    space1[loc1++] = ch1;
                    marker1++;

                    if (marker1 < len1) {
                        ch1 = x[marker1];
                    } else {
                        break;
                    }
                } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

                do {
                    space2[loc2++] = ch2;
                    marker2++;

                    if (marker2 < len2) {
                        ch2 = y[marker2];
                    } else {
                        break;
                    }
                } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                var str1 = new string(space1);
                var str2 = new string(space2);

                int result;

                if (char.IsDigit(space1[0]) && char.IsDigit(space2[0])) {
                    var thisNumericChunk = int.Parse(str1, CultureInfo.InvariantCulture);
                    var thatNumericChunk = int.Parse(str2, CultureInfo.InvariantCulture);
                    result = thisNumericChunk.CompareTo(thatNumericChunk);
                } else {
                    result = string.Compare(str1, str2, StringComparison.Ordinal);
                }

                if (result != 0) {
                    return result;
                }
            }
            return len1 - len2;
        }

        public static int Compare(object x, object y) {
            var s1 = x as string;
            if (s1 == null) {
                return 0;
            }

            var s2 = y as string;
            if (s2 == null) {
                return 0;
            }

            return Compare(s1, s2);
        }
    }

    public static class StringExtension {
        public static int CompareAsVersionTo(this string a, string b) {
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

        public static bool IsVersionNewerThan(this string currentVersion, string checkableVersion) {
            return currentVersion.CompareAsVersionTo(checkableVersion) > 0;
        }

        public static bool IsVersionOlderThan(this string currentVersion, string checkableVersion) {
            return currentVersion.CompareAsVersionTo(checkableVersion) < 0;
        }

        public static bool Contains(this string s, string sub, StringComparison comparison) {
            return s.IndexOf(sub, comparison) != -1;
        }

        public static string SubstringExt(this string s, int from) {
            if (from < 0) {
                from = s.Length - from;
            }

            if (from > s.Length) {
                return string.Empty;
            }

            return from <= 0 ? s : s.Substring(from);
        }

        public static string ApartFromLast(this string s, int apart) {
            if (apart < 0) {
                apart = s.Length - apart;
            }

            if (apart > s.Length) {
                return string.Empty;
            }

            return apart <= 0 ? s : s.Substring(0, s.Length - apart);
        }

        public static string ApartFromFirst(this string s, string apart) {
            if (apart == string.Empty) return s;
            return s.StartsWith(apart) ? s.Substring(apart?.Length ?? 0) : s;
        }

        public static string ApartFromFirst(this string s, string apart, StringComparison comparisonType) {
            if (apart == string.Empty) return s;
            return s.StartsWith(apart, comparisonType) ? s.Substring(apart?.Length ?? 0) : s;
        }

        public static string ApartFromLast(this string s, string apart) {
            if (apart == string.Empty) return s;
            return s.EndsWith(apart) ? s.ApartFromLast(apart?.Length ?? 0) : s;
        }

        public static string ApartFromLast(this string s, string apart, StringComparison comparisonType) {
            if (apart == string.Empty) return s;
            return s.EndsWith(apart, comparisonType) ? s.ApartFromLast(apart?.Length ?? 0) : s;
        }

        [Pure]
        [NotNull]
        public static string ReplaceLastOccurrence([NotNull]this string s, string what, string replacement) {
            var place = s.LastIndexOf(what, StringComparison.Ordinal);
            return place == -1 ? s : s.Remove(place, what.Length).Insert(place, replacement);
        }

        /// <summary>
        /// Convert bytes to using UTF8 (only if it’s a correct one) or Default encoding.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [Pure]
        [NotNull]
        public static string ToUtf8String([NotNull]this byte[] bytes) {
            return (UTF8Checker.IsUtf8(bytes, 200) ? Encoding.UTF8 : Encoding.Default).GetString(bytes);
        }

        /// <summary>
        /// Only in about two times slower than Enumerable.Range.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        [Pure]
        public static IEnumerable<int> ToDiapason(this string s, int min, int max) {
            if (min < 0) throw new ArgumentOutOfRangeException(nameof(min), @"Negative numbers aren’t supported");
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max));

            return s.Split(',').SelectMany(part => {
                int n = part.IndexOf('-'), fromValue, toValue;
                if (n > 0 && n < part.Length - 1) { // "x-y"
                    if (int.TryParse(part.Substring(0, n), out fromValue) && int.TryParse(part.Substring(n + 1), out toValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + Math.Min(toValue, max) - fromValue);
                    }
                } else if (n < 0) { // "x"
                    if (int.TryParse(part, out fromValue) && fromValue >= min && fromValue <= max) {
                        return new[] { fromValue };
                    }
                } else if (n == part.Length - 1) { // "x-"
                    if (int.TryParse(part.Substring(0, n), out fromValue)) {
                        fromValue = Math.Max(fromValue, min);
                        return Enumerable.Range(fromValue, 1 + max - fromValue);
                    }
                } else if (part.Length == 1) { // "-"
                    return Enumerable.Range(min, 1 + max - min);
                } else { // "-x"
                    if (int.TryParse(part.Substring(n + 1), out toValue)) {
                        return Enumerable.Range(min, 1 + Math.Min(toValue, max) - min);
                    }
                }

                return new int[0];
            });
        }
    }
}
