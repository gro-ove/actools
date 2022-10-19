// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace AcTools.Numerics {
    public static class HashCodeHelper {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes(uint value) {
            return (value & 0x0000FFFFU) << 16 | (value & 0xFFFF0000U) >> 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ReverseBytes(int value) {
            var r = ReverseBytes(*(uint*)&value);
            return *(int*)&r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CombineHashCodes(int h1, int h2) {
            h1 = ReverseBytes(h1);
            unchecked {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CombineHashCodes(uint h1, uint h2) {
            h1 = ReverseBytes(h1);
            unchecked {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int CombineHashCodes(float h1, float h2, float h3) {
            var r = CombineHashCodes(*(uint*)&h1, CombineHashCodes(*(uint*)&h2, *(uint*)&h3));
            return *(int*)&r;
        }
    }
}