using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ArrayExtension {
        [CanBeNull]
        public static T[] CreateArrayOfType<T>(int size) where T : new() {
            var result = new T[size];
            for (var i = 0; i < result.Length; i++) {
                result[i] = new T();
            }
            return result;
        }

        [CanBeNull]
        public static TBase[] CreateArrayOfType<TBase, TValue>(int size) where TValue : TBase, new() {
            var result = new TBase[size];
            for (var i = 0; i < result.Length; i++) {
                result[i] = new TValue();
            }
            return result;
        }

        public static bool ArrayContains<T>([NotNull] this T[] array, T value) {
            return Array.IndexOf(array, value) != -1;
        }

        [CanBeNull]
        public static T ArrayElementAtOrDefault<T>([NotNull] this T[] array, int index) {
            return index >= 0 && index < array.Length ? array[index] : default(T);
        }

        [NotNull]
        public static T[] Slice<T>([NotNull] this T[] array, int startIndex, int length) {
            if (startIndex == 0 && length == array.Length) {
                return array;
            }

            var subset = new T[length];
            Array.Copy(array, startIndex, subset, 0, length);
            return subset;
        }

        [NotNull]
        public static T[] Copy<T>([NotNull] this T[] array) where T : struct {
            var copy = new T[array.Length];
            Array.Copy(array, 0, copy, 0, array.Length);
            return copy;
        }

        [NotNull]
        public static T[] Subset<T>([NotNull] this T[] array, [NotNull] params int[] indices) {
            var subset = new T[indices.Length];
            for (var i = 0; i < indices.Length; i++) {
                subset[i] = array[indices[i]];
            }
            return subset;
        }

        [DllImport("msvcrt.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        public static bool EqualsTo(this byte[] b1, byte[] b2) {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        public static bool StartsWith([NotNull] this byte[] b1, [NotNull] params byte[] b2) {
            if (b1.Length < b2.Length) return false;
            for (var i = 0; i < b2.Length; i++) {
                if (b1[i] != b2[i]) return false;
            }
            return true;
        }

        public static string ToHexString([NotNull] this byte[] data) {
            var lookup = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            int i = -1, p = -1, l = data.Length;
            var c = new char[l-- * 2];
            while (i < l) {
                var d = data[++i];
                c[++p] = lookup[d >> 4];
                c[++p] = lookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }

        [Pure, CanBeNull]
        public static byte[] FromCutBase64([CanBeNull] this string encoded) {
            if (!string.IsNullOrWhiteSpace(encoded)) {
                try {
                    var padding = 4 - encoded.Length % 4;
                    if (padding > 0 && padding < 4) {
                        encoded = encoded + "=".RepeatString(padding);
                    }

                    return Convert.FromBase64String(encoded);
                } catch (Exception e) {
                    AcToolsLogging.Write(">" + encoded + "<");
                    AcToolsLogging.Write(e);
                }
            }

            return null;
        }

        [Pure, CanBeNull]
        public static string ToCutBase64([CanBeNull] this byte[] decoded) {
            if (decoded != null) {
                try {
                    return Convert.ToBase64String(decoded).TrimEnd('=');
                } catch (Exception e) {
                    AcToolsLogging.Write(e);
                }
            }

            return null;
        }
    }
}