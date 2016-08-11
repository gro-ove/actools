/*
 * Improved version to C# LibLZF Port:
 * Copyright (c) 2010 Roman Atachiants <kelindar@gmail.com>
 * 
 * Original CLZF Port:
 * Copyright (c) 2005 Oren J. Maurice <oymaurice@hazorea.org.il>
 * 
 * Original LibLZF Library & Algorithm:
 * Copyright (c) 2000-2008 Marc Alexander Lehmann <schmorp@schmorp.de>
 * 
 * Redistribution and use in source and binary forms, with or without modifica-
 * tion, are permitted provided that the following conditions are met:
 * 
 *   1.  Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 * 
 *   2.  Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 * 
 *   3.  The name of the author may not be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MER-
 * CHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO
 * EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPE-
 * CIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTH-
 * ERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * Alternatively, the contents of this file may be used under the terms of
 * the GNU General Public License version 2 (the "GPL"), in which case the
 * provisions of the GPL are applicable instead of the above. If you wish to
 * allow the use of your version of this file only under the terms of the
 * GPL and not to allow others to use your version of this file under the
 * BSD license, indicate your decision by deleting the provisions above and
 * replace them with the notice and other provisions required by the GPL. If
 * you do not delete the provisions above, a recipient may use your version
 * of this file under either the BSD or the GPL.
 */

using System;
using System.Collections.Generic;

namespace FirstFloor.ModernUI.Helpers {
    /* Benchmark with Alice29 Canterbury Corpus
        ---------------------------------------
        (Compression) Original CLZF C#
        Raw = 152089, Compressed = 101092
         8292,4743 ms.
        ---------------------------------------
        (Compression) My LZF C#
        Raw = 152089, Compressed = 101092
         33,0019 ms.
        ---------------------------------------
        (Compression) Zlib using SharpZipLib
        Raw = 152089, Compressed = 54388
         8389,4799 ms.
        ---------------------------------------
        (Compression) QuickLZ C#
        Raw = 152089, Compressed = 83494
         80,0046 ms.
        ---------------------------------------
        (Decompression) Original CLZF C#
        Decompressed = 152089
         16,0009 ms.
        ---------------------------------------
        (Decompression) My LZF C#
        Decompressed = 152089
         15,0009 ms.
        ---------------------------------------
        (Decompression) Zlib using SharpZipLib
        Decompressed = 152089
         3577,2046 ms.
        ---------------------------------------
        (Decompression) QuickLZ C#
        Decompressed = 152089
         21,0012 ms.
    */


    /// <summary>
    /// Improved C# LZF Compressor, a very small data compression library. The compression algorithm is extremely fast. 
    /// </summary>
    internal sealed class Lzf {
        /// <summary>
        /// Hashtable, thac can be allocated only once
        /// </summary>
        private static readonly long[] HashTable = new long[Hsize];

        private const uint Hlog = 14;
        private const uint Hsize = 1 << 14;
        private const uint MaxLit = 1 << 5;
        private const uint MaxOff = 1 << 13;
        private const uint MaxRef = (1 << 8) + (1 << 3);

        /// <summary>
        /// Compresses the data using LibLZF algorithm
        /// </summary>
        /// <param name="input">Reference to the data to compress</param>
        /// <param name="inputLength">Lenght of the data to compress</param>
        /// <param name="output">Reference to a buffer which will contain the compressed data</param>
        /// <param name="outputLength">Lenght of the compression buffer (should be bigger than the input buffer)</param>
        /// <returns>The size of the compressed archive in the output buffer</returns>
        public static int Compress(byte[] input, int inputLength, byte[] output, int outputLength) {
            lock (HashTable) {
                Array.Clear(HashTable, 0, (int)Hsize);

                uint iidx = 0;
                uint oidx = 0;

                var hval = (uint)((input[iidx] << 8) | input[iidx + 1]);
                var lit = 0;

                for (;;) {
                    if (iidx < inputLength - 2) {
                        hval = (hval << 8) | input[iidx + 2];
                        long hslot = (hval ^ (hval << 5)) >> (int)(3 * 8 - Hlog - hval * 5) & (Hsize - 1);
                        var reference = HashTable[hslot];
                        HashTable[hslot] = iidx;

                        long off;
                        if ((off = iidx - reference - 1) < MaxOff
                                && iidx + 4 < inputLength
                                && reference > 0
                                && input[reference + 0] == input[iidx + 0]
                                && input[reference + 1] == input[iidx + 1]
                                && input[reference + 2] == input[iidx + 2]) {
                            /* match found at *reference++ */
                            uint len = 2;
                            var maxlen = (uint)inputLength - iidx - len;
                            maxlen = maxlen > MaxRef ? MaxRef : maxlen;

                            if (oidx + lit + 1 + 3 >= outputLength) return 0;

                            do {
                                len++;
                            } while (len < maxlen && input[reference + len] == input[iidx + len]);

                            if (lit != 0) {
                                output[oidx++] = (byte)(lit - 1);
                                lit = -lit;
                                do {
                                    output[oidx++] = input[iidx + lit];
                                } while (++lit != 0);
                            }

                            len -= 2;
                            iidx++;

                            if (len < 7) {
                                output[oidx++] = (byte)((off >> 8) + (len << 5));
                            } else {
                                output[oidx++] = (byte)((off >> 8) + (7 << 5));
                                output[oidx++] = (byte)(len - 7);
                            }

                            output[oidx++] = (byte)off;

                            iidx += len - 1;
                            hval = (uint)((input[iidx] << 8) | input[iidx + 1]);

                            hval = (hval << 8) | input[iidx + 2];
                            HashTable[(hval ^ (hval << 5)) >> (int)(3 * 8 - Hlog - hval * 5) & (Hsize - 1)] = iidx;
                            iidx++;

                            hval = (hval << 8) | input[iidx + 2];
                            HashTable[(hval ^ (hval << 5)) >> (int)(3 * 8 - Hlog - hval * 5) & (Hsize - 1)] = iidx;
                            iidx++;
                            continue;
                        }
                    } else if (iidx == inputLength) break;

                    /* one more literal byte we must copy */
                    lit++;
                    iidx++;

                    if (lit == MaxLit) {
                        if (oidx + 1 + MaxLit >= outputLength) return 0;

                        output[oidx++] = (byte)(MaxLit - 1);
                        lit = -lit;
                        do {
                            output[oidx++] = input[iidx + lit];
                        } while (++lit != 0);
                    }
                }

                if (lit != 0) {
                    if (oidx + lit + 1 >= outputLength) return 0;

                    output[oidx++] = (byte)(lit - 1);
                    lit = -lit;
                    do {
                        output[oidx++] = input[iidx + lit];
                    } while (++lit != 0);
                }

                return (int)oidx;
            }
        }

        public static byte[] Compress(byte[] input) {
            if (input.Length == 0) return new byte[0];

            for (var i = 1; i < 10; i++) {
                var result = new byte[input.Length * i];
                var length = Compress(input, input.Length, result, result.Length);
                if (length == 0) continue;
                if (length == input.Length * i) return result;

                var slice = new byte[length];
                Array.Copy(result, 0, slice, 0, length);
                return slice;
            }

            throw new Exception("Can’t compress data");
        }

        public static byte[] CompressWithPrefix(byte[] input, byte flag) {
            if (input.Length == 0) return new byte[0];

            for (var i = 1; i < 10; i++) {
                var result = new byte[input.Length * i];
                var length = Compress(input, input.Length, result, result.Length);
                if (length == 0) continue;
                if (length == input.Length * i) return result;

                var slice = new byte[length + 1];
                slice[0] = flag;
                Array.Copy(result, 0, slice, 1, length);
                return slice;
            }

            throw new Exception("Can’t compress data");
        }


        /// <summary>
        /// Decompresses the data using LibLZF algorithm
        /// </summary>
        /// <param name="input">Reference to the data to decompress</param>
        /// <param name="inputOffset">Starting offset</param>
        /// <param name="inputLength">Length of the data to decompress</param>
        /// <param name="output">Reference to a buffer which will contain the decompressed data</param>
        /// <param name="outputLength">The size of the decompressed archive in the output buffer</param>
        /// <returns>Returns decompressed size</returns>
        public static int Decompress(byte[] input, int inputOffset, int inputLength, byte[] output, int outputLength) {
            var iidx = (uint)inputOffset;
            uint oidx = 0;

            do {
                uint ctrl = input[iidx++];

                if (ctrl < 1 << 5) {
                    /* literal run */
                    ctrl++;

                    //SET_ERRNO (E2BIG);
                    if (oidx + ctrl > outputLength) return 0;
                    do {
                        output[oidx++] = input[iidx++];
                    } while (--ctrl != 0);
                } else {
                    /* back reference */
                    var len = ctrl >> 5;
                    var reference = (int)(oidx - ((ctrl & 0x1f) << 8) - 1);
                    if (len == 7) len += input[iidx++];
                    reference -= input[iidx++];

                    //SET_ERRNO (E2BIG);
                    if (oidx + len + 2 > outputLength) return 0;

                    //SET_ERRNO (EINVAL);
                    if (reference < 0) return 0;

                    output[oidx++] = output[reference++];
                    output[oidx++] = output[reference++];

                    do {
                        output[oidx++] = output[reference++];
                    } while (--len != 0);
                }
            } while (iidx < inputLength);
            return (int)oidx;
        }

        /// <summary>
        /// Bad solution, but still.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static byte[] Decompress(byte[] input, int offset, int count) {
            if (input.Length == 0) return new byte[0];

            var result = new List<byte>(count * 2);
            do {
                uint ctrl = input[offset++];
                if (ctrl < 1 << 5) {
                    /* literal run */
                    ctrl++;

                    do {
                        result.Add(input[offset++]);
                    } while (--ctrl != 0);
                } else {
                    /* back reference */
                    var len = ctrl >> 5;
                    var reference = (int)(result.Count - ((ctrl & 0x1f) << 8) - 1);
                    if (len == 7) len += input[offset++];
                    reference -= input[offset++];

                    if (reference < 0) throw new Exception("Negative reference");

                    result.Add(result[reference++]);
                    result.Add(result[reference++]);

                    do {
                        result.Add(result[reference++]);
                    } while (--len != 0);
                }
            } while (offset < count);
            return result.ToArray();
        }
    }
}

