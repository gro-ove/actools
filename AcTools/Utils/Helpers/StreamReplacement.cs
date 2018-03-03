using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AcTools.Utils.Helpers {
    public class StreamReplacement {
        private readonly Encoding _encoding = Encoding.UTF8;

        private class Replacement {
            public readonly byte[] Find;
            public readonly byte[] ReplaceBy;
            public int MatchOffset;

            public Replacement(string find, string replaceBy, Encoding encoding) {
                Find = encoding.GetBytes(find);
                ReplaceBy = encoding.GetBytes(replaceBy);
            }

            public Replacement(byte[] find, byte[] replaceBy) {
                Find = find;
                ReplaceBy = replaceBy;
            }
        }

        private readonly Replacement[] _replacements;

        private readonly List<byte> _overflow = new List<byte>();

        public StreamReplacement(IEnumerable<KeyValuePair<string, string>> replacements, Encoding encoding = null) {
            _encoding = encoding ?? _encoding;
            _replacements = replacements.Select(x => new Replacement(x.Key, x.Value, _encoding)).ToArray();
        }

        public StreamReplacement(IEnumerable<KeyValuePair<byte[], byte[]>> replacements, Encoding encoding = null) {
            _encoding = encoding ?? _encoding;
            _replacements = replacements.Select(x => new Replacement(x.Key, x.Value)).ToArray();
        }

        public StreamReplacement(params KeyValuePair<string, string>[] replacements) : this((IEnumerable<KeyValuePair<string, string>>)replacements) { }
        public StreamReplacement(string find, string replaceBy) : this(new KeyValuePair<string, string>(find, replaceBy)) { }

        public StreamReplacement(params KeyValuePair<byte[], byte[]>[] replacements) : this((IEnumerable<KeyValuePair<byte[], byte[]>>)replacements) { }
        public StreamReplacement(byte[] find, byte[] replaceBy) : this(new KeyValuePair<byte[], byte[]>(find, replaceBy)) { }

        public string Filter(string dataIn) {
            return _encoding.GetString(Filter(_encoding.GetBytes(dataIn)));
        }

        public byte[] Filter(byte[] dataIn) {
            using (var input = new MemoryStream(dataIn))
            using (var output = new MemoryStream(dataIn.Length)) {
                Filter(input, output);
                return output.ToArray();
            }
        }

        public bool Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten) {
            return Filter(dataIn, out dataInRead, dataOut, out dataOutWritten, true);
        }

        public void Filter(Stream dataIn, Stream dataOut) {
            Filter(dataIn, out _, dataOut, out _, false);
        }

        private bool Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten, bool limitedDataOut) {
            dataOutWritten = 0;

            if (dataIn == null) {
                dataInRead = 0;
                return true;
            }

            dataInRead = dataIn.Length;
            if (_overflow.Count > 0) {
                WriteOverflow(dataOut, ref dataOutWritten);
            }

            var replacements = _replacements;

            for (var i = 0; i < dataInRead; ++i) {
                var readByte = (byte)dataIn.ReadByte();

                var matched = false;
                var matchedLength = 0;
                byte[] respondWith = null;
                var respondWithLength = 0;

                for (var j = replacements.Length - 1; j >= 0; j--) {
                    var current = replacements[j];

                    if (readByte == current.Find[current.MatchOffset]) {
                        matched = true;
                        matchedLength = Math.Max(++current.MatchOffset, matchedLength);
                        if (current.MatchOffset == current.Find.Length) {
                            WriteBytes(current.ReplaceBy, current.ReplaceBy.Length, dataOut, ref dataOutWritten, limitedDataOut);
                            for (var k = replacements.Length - 1; k >= 0; k--) {
                                replacements[k].MatchOffset = 0;
                            }
                            break;
                        }
                    } else {
                        if (current.MatchOffset > 0) {
                            if (current.MatchOffset > respondWithLength) {
                                respondWith = current.Find;
                                respondWithLength = current.MatchOffset;
                            }
                            current.MatchOffset = 0;
                        }
                    }
                }

                if (matched) {
                    if (respondWith != null && respondWithLength > matchedLength) {
                        WriteBytes(respondWith, respondWithLength - matchedLength, dataOut, ref dataOutWritten, limitedDataOut);
                    }
                } else {
                    if (respondWith != null) {
                        WriteBytes(respondWith, respondWithLength, dataOut, ref dataOutWritten, limitedDataOut);
                    }
                    WriteSingleByte(readByte, dataOut, ref dataOutWritten, limitedDataOut);
                }
            }

            if (_overflow.Count > 0) {
                return false;
            }

            for (var j = replacements.Length - 1; j >= 0; j--) {
                if (replacements[j].MatchOffset > 0) {
                    return false;
                }
            }

            return true;
        }

        private void WriteOverflow(Stream dataOut, ref long dataOutWritten) {
            var remainingSpace = dataOut.Length - dataOutWritten;
            var maxWrite = Math.Min(_overflow.Count, remainingSpace);

            if (maxWrite > 0) {
                dataOut.Write(_overflow.ToArray(), 0, (int)maxWrite);
                dataOutWritten += maxWrite;
            }

            if (maxWrite < _overflow.Count) {
                _overflow.RemoveRange(0, (int)(maxWrite - 1));
            } else {
                _overflow.Clear();
            }
        }

        private void WriteBytes(byte[] bytes, int bytesCount, Stream dataOut, ref long dataOutWritten, bool limitedDataOut) {
            if (!limitedDataOut) {
                dataOut.Write(bytes, 0, bytesCount);
                return;
            }

            var remainingSpace = dataOut.Length - dataOutWritten;
            var maxWrite = Math.Min(bytesCount, remainingSpace);

            if (maxWrite > 0) {
                dataOut.Write(bytes, 0, (int)maxWrite);
                dataOutWritten += maxWrite;
            }

            if (maxWrite < bytesCount) {
                var range = new byte[bytesCount - maxWrite];
                Array.Copy(bytes, maxWrite, range, 0, range.LongLength);
                _overflow.AddRange(range);
            }
        }

        private void WriteSingleByte(byte data, Stream dataOut, ref long dataOutWritten, bool limitedDataOut) {
            if (!limitedDataOut) {
                dataOut.WriteByte(data);
                return;
            }

            var remainingSpace = dataOut.Length - dataOutWritten;

            if (remainingSpace > 0) {
                dataOut.WriteByte(data);
                dataOutWritten += 1;
            } else {
                _overflow.Add(data);
            }
        }
    }
}