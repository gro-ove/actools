using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AcTools;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    internal sealed class ReplayReader : ReadAheadBinaryReader {
        public ReplayReader(string filename)
                : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192)) { }

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

        private const int BufferSize = 2048;
        private const int BytesRead = BufferSize + 4;
        private readonly byte[] _buffer = new byte[BufferSize + 4];

        [CanBeNull]
        public string TryToReadNextString() {
            var p = new byte[4];
            for (var j = 0;; j++) {
                try {
                    if (j == 0) {
                        ReadBytesTo(p, 0, p.Length);
                    } else {
                        Array.Copy(_buffer, _buffer.Length - 4, p, 0, 4);
                    }

                    ReadBytesTo(_buffer, 4, BufferSize);
                } catch (EndOfStreamException) {
                    return null;
                }

                var i = 3;
                while (i < BytesRead) {
                    if (!IsStringCharacter(_buffer[i])) {
                        i += 4;
                    } else if (!IsStringCharacter(_buffer[i - 1])) {
                        i += 3;
                    } else if (!IsStringCharacter(_buffer[i - 2])) {
                        i += 2;
                    } else {
                        if (IsStringCharacter(_buffer[i - 3])) {
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
                                le = BitConverter.ToInt32(_buffer, lstart);
                            }

                            if (le < 3 || le > 120) {
                                ++i;
                                continue;
                            }

                            var sb = new StringBuilder();
                            for (i = i - 3; i < BytesRead; i++) {
                                var n = _buffer[i];
                                if (IsStringCharacter(n)) {
                                    sb.Append((char)n);
                                } else {
                                    break;
                                }
                            }

                            if (i == BytesRead) {
                                int n;
                                while ((n = ReadByte()) != 0) {
                                    if (IsStringCharacter(n)) {
                                        sb.Append((char)n);
                                    } else {
                                        Seek(-1, SeekOrigin.Current);
                                        break;
                                    }
                                }
                            } else {
                                Seek(i - BytesRead, SeekOrigin.Current);
                            }

                            var candidate = sb.ToString();
                            if (candidate.Length == le) {
                                return candidate;
                            }

                        }

                        ++i;
                    }
                }

                _buffer[0] = _buffer[BufferSize];
                _buffer[1] = _buffer[BufferSize + 1];
                _buffer[2] = _buffer[BufferSize + 2];
                _buffer[3] = _buffer[BufferSize + 3];
            }
        }

        /// <summary>
        /// Filter strings by two first characters to make search faster. More characters to filter by
        /// only make it worse.
        /// </summary>
        [CanBeNull]
        public string TryToReadNextString(byte f0, byte f1) {
            var p = new byte[4];
            for (var j = 0; j < 10000; j++) {
                try {
                    if (j == 0) {
                        ReadBytesTo(p, 0, p.Length);
                    } else {
                        Array.Copy(_buffer, _buffer.Length - 4, p, 0, 4);
                    }

                    ReadBytesTo(_buffer, 4, BufferSize);
                } catch (EndOfStreamException) {
                    return null;
                }

                var i = 3;
                while (i < BytesRead - 1) {
                    var n = _buffer[i + 1];
                    if (_buffer[i] == f0 && n == f1) break;
                    i += n != f0 ? 2 : 1;
                }

                while (i < BytesRead) {
                    if (!IsStringCharacter(_buffer[i])) {
                        i += 4;
                    } else if (!IsStringCharacter(_buffer[i - 1])) {
                        i += 3;
                    } else if (!IsStringCharacter(_buffer[i - 2])) {
                        i += 2;
                    } else {
                        if (IsStringCharacter(_buffer[i - 3])) {
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
                                le = BitConverter.ToInt32(_buffer, lstart);
                            }

                            if (le < 3 || le > 120) {
                                ++i;
                                continue;
                            }

                            var sb = new StringBuilder();
                            for (i = i - 3; i < BytesRead; i++) {
                                var n = _buffer[i];
                                if (IsStringCharacter(n)) {
                                    sb.Append((char)n);
                                } else {
                                    break;
                                }
                            }

                            if (i == BytesRead) {
                                int n;
                                while ((n = ReadByte()) != 0) {
                                    if (IsStringCharacter(n)) {
                                        sb.Append((char)n);
                                    } else {
                                        Seek(-1, SeekOrigin.Current);
                                        break;
                                    }
                                }
                            } else {
                                Seek(i - BytesRead, SeekOrigin.Current);
                            }

                            var candidate = sb.ToString();
                            if (candidate.Length == le) {
                                return candidate;
                            }

                        }

                        ++i;
                    }
                }

                _buffer[0] = _buffer[BufferSize];
                _buffer[1] = _buffer[BufferSize + 1];
                _buffer[2] = _buffer[BufferSize + 2];
                _buffer[3] = _buffer[BufferSize + 3];
            }

            return null;
        }

        public new string ReadString() {
            return ReadString(256);
        }
    }
}