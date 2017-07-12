using System;
using System.Runtime.InteropServices;

namespace AcTools.Utils.Helpers {
    public static class ArrayExtension {
        public static T[] RangeSubset<T>(this T[] array, int startIndex, int length) {
            var subset = new T[length];
            Array.Copy(array, startIndex, subset, 0, length);
            return subset;
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
    }
}