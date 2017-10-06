using System;
using System.Runtime.InteropServices;

namespace AcTools.Utils.Helpers {
    public static class ArrayExtension {
        public static T[] Slice<T>(this T[] array, int startIndex, int length) {
            if (startIndex == 0 && length == array.Length) {
                return array;
            }

            var subset = new T[length];
            Array.Copy(array, startIndex, subset, 0, length);
            return subset;
        }

        public static T[] Copy<T>(this T[] array) where T : struct {
            var copy = new T[array.Length];
            Array.Copy(array, 0, copy, 0, array.Length);
            return copy;
        }

        public static T[] Subset<T>(this T[] array, params int[] indices) {
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

        public static bool StartsWith(this byte[] b1, byte[] b2) {
            if (b1.Length < b2.Length) return false;
            for (var i = 0; i < b2.Length; i++) {
                if (b1[i] != b2[i]) return false;
            }
            return true;
        }

        public static string ToHexString(this byte[] data) {
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
    }
}