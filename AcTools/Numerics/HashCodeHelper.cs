// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace AcTools.Numerics {
    internal static class HashCodeHelper {
        internal static int CombineHashCodes(int h1, int h2) {
            return ((h1 << 5) + h1) ^ h2;
        }
    }
}