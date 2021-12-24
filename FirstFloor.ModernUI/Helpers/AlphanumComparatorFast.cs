using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class AlphanumComparatorFast : IComparer, IComparer<string> {
        public static AlphanumComparatorFast Instance { get; } = new AlphanumComparatorFast();

        int IComparer.Compare(object x, object y) {
            return Compare(x, y);
        }

        int IComparer<string>.Compare(string x, string y) {
            return Compare(x, y);
        }

        private static bool IsDigit(char c) {
            return c >= '0' && c <= '9';
        }

        private static int CompareBit(char[] a, int al, char[] b, int bl, bool ignoreCase) {
            var l = Math.Min(al, bl);
            for (var i = 0; i < l; ++i) {
                var ac = a[i];
                var bc = b[i];
                if (ignoreCase) {
                    ac = char.ToLowerInvariant(ac);
                    bc = char.ToLowerInvariant(bc);
                }
                var d = ac - bc;
                if (d != 0) return d;
            }
            return al - bl;
        }

        private static int CompareNumericBit(char[] a, int al, char[] b, int bl) {
            if (al != bl) return al - bl;
            for (var i = 0; i < al; ++i) {
                var d = a[i] - b[i];
                if (d != 0) return d;
            }
            return 0;
        }

        public static int Compare([CanBeNull] string x, [CanBeNull] string y, bool ignoreCase = true) {
            if (x == null) return y == null ? 0 : 1;
            if (y == null) return -1;

            var len1 = x.Length;
            var len2 = y.Length;
            var marker1 = 0;
            var marker2 = 0;

            // Some buffers we can build up characters in for each chunk.
            var space1 = new char[len1];
            var space2 = new char[len2];

            // Walk through two the strings with two markers.
            while (marker1 < len1 && marker2 < len2) {
                var ch1 = x[marker1];
                var ch2 = y[marker2];

                // Walk through all following characters that are digits or
                // characters in BOTH strings starting at the appropriate marker.
                // Collect char arrays.
                var loc1 = 0;
                var num1 = IsDigit(ch1);
                do {
                    space1[loc1] = ch1;
                    if (loc1 > 0 || ch1 != '0') ++loc1;
                    marker1++;

                    if (marker1 < len1) {
                        ch1 = x[marker1];
                    } else {
                        break;
                    }
                } while (IsDigit(ch1) == num1);

                var loc2 = 0;
                var num2 = IsDigit(ch2);
                do {
                    space2[loc2] = ch2;
                    if (loc2 > 0 || ch2 != '0') ++loc2;
                    marker2++;

                    if (marker2 < len2) {
                        ch2 = y[marker2];
                    } else {
                        break;
                    }
                } while (IsDigit(ch2) == num2);

                // If we have collected numbers, compare them numerically.
                // Otherwise, if we have strings, compare them alphabetically.
                var result = num1 && num2 ? CompareNumericBit(space1, loc1, space2, loc2)
                        : CompareBit(space1, loc1, space2, loc2, ignoreCase);
                if (result != 0) {
                    return result;
                }
            }
            return len1 - len2;
        }

        public static int Compare(object x, object y, bool ignoreCase = true) {
            if (x is string s1 && y is string s2) {
                return Compare(s1, s2, ignoreCase);
            }
            return 0;
        }
    }
}