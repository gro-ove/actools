using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    internal sealed class ReplayReader : ReadAheadBinaryReader {
        public ReplayReader(string filename)
                : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096)) { }

        public ReplayReader(Stream input)
                : base(input) { }

        public string ReadString(int limit) {
            var length = ReadInt32();
            if (length > limit) {
                throw new Exception(ToolsStrings.ReplayReader_UnsupportedFormat);
            }

            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsStringCharacter(int c) {
            return c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-' || c >= 'A' && c <= 'Z';
        }

        [CanBeNull]
        public string TryToReadNextString() {
            const int bufferSize = 512;
            const int bytesRead = bufferSize + 4;

            var b = new byte[bufferSize + 4];
            var p = new byte[4];
            for (var j = 0;; j++) {
                try {
                    if (j == 0) {
                        ReadBytesTo(p, 0, p.Length);
                    } else {
                        Array.Copy(b, b.Length - 4, p, 0, 4);
                    }

                    ReadBytesTo(b, 4, bufferSize);
                } catch (EndOfStreamException) {
                    return null;
                }

                var i = 3;
                while (i < bytesRead) {
                    if (!IsStringCharacter(b[i])) {
                        i += 4;
                    } else if (!IsStringCharacter(b[i - 1])) {
                        i += 3;
                    } else if (!IsStringCharacter(b[i - 2])) {
                        i += 2;
                    } else {
                        if (IsStringCharacter(b[i - 3])) {
                            var lstart = i - 7;
                            int le;
                            if (lstart < 0) {
                                if (lstart > -4) {
                                    for (var m = 0; m < -lstart; m++) {
                                        p[m] = p[m + 4 + lstart];
                                    }
                                }

                                le = BitConverter.ToInt32(p, 0);
                            } else {
                                le = BitConverter.ToInt32(b, lstart);
                            }

                            if (le < 3 || le > 120) {
                                ++i;
                                continue;
                            }

                            var sb = new StringBuilder();
                            for (i = i - 3; i < bytesRead; i++) {
                                var n = b[i];
                                if (IsStringCharacter(n)) {
                                    sb.Append((char)n);
                                } else {
                                    break;
                                }
                            }

                            if (i == bytesRead) {
                                int n;
                                while ((n = ReadByte()) != -1) {
                                    if (IsStringCharacter(n)) {
                                        sb.Append((char)n);
                                    } else {
                                        Seek(-1, SeekOrigin.Current);
                                        break;
                                    }
                                }
                            } else {
                                Seek(i - bytesRead, SeekOrigin.Current);
                            }

                            var candidate = sb.ToString();
                            if (candidate.Length == le) {
                                return candidate;
                            }

                        }

                        ++i;
                    }
                }

                b[0] = b[bufferSize];
                b[1] = b[bufferSize + 1];
                b[2] = b[bufferSize + 2];
                b[3] = b[bufferSize + 3];
            }
        }

        public new string ReadString() {
            return ReadString(256);
        }
    }
}