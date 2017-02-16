using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;

namespace AcTools {
    internal class ReadAheadBinaryReader : IDisposable {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _left, _total;
        private long? _length;

        public long Position => _stream.Position - _left;

        public long Length => _length ?? (_length = _stream.Length).Value;

        public ReadAheadBinaryReader BaseStream => this;

        public ReadAheadBinaryReader(string filename, int bufferSize = 2048) {
            if (!File.Exists(filename)) throw new FileNotFoundException("KN5 file is missing", filename);
            _stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 8196);
            _buffer = new byte[bufferSize];
            _left = 0;
        }

        public ReadAheadBinaryReader(Stream stream, int bufferSize = 2048) {
            _stream = stream;
            _buffer = new byte[bufferSize];
            _left = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte NextByte() {
            return _buffer[_total - _left--];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Require(int count) {
            if (_left < count) {
                if (_left > 0) {
                    Array.Copy(_buffer, _total - _left, _buffer, 0, _left);
                }

                if (_left < 0) _left = 0;

                var leftToFill = _buffer.Length - _left;
                _left += _stream.Read(_buffer, _left, leftToFill);
                _total = _left;

                if (_left < count) throw new Exception("Unexpected end.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPosAndMove(int count) {
            Require(count);
            var p = _total - _left;
            _left -= count;
            return p;
        }

        public char[] ReadChars(int count) {
            Require(count);

            var result = new char[count];
            for (var i = 0; i < count; i++) {
                result[i] = (char)NextByte();
            }

            return result;
        }

        public byte ReadByte() {
            Require(1);
            return NextByte();
        }

        public string ReadString() {
            var length = ReadInt32();
            return Encoding.ASCII.GetString(_buffer, GetPosAndMove(length), length);
        }

        #region Some convertation stuff
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ToInt322(byte[] value, int startIndex) {
            fixed (byte* pbyte = &value[startIndex]) {
                if (startIndex % 4 == 0) return *(int*)pbyte;
                return BitConverter.IsLittleEndian
                        ? *pbyte | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24)
                        : (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | *(pbyte + 3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ToInt32(byte[] value, int startIndex) {
            fixed (byte* pbyte = &value[startIndex]) {
                if (startIndex % 4 == 0) return *(int*)pbyte;
                return BitConverter.IsLittleEndian
                        ? *pbyte | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24)
                        : (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | *(pbyte + 3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float ToSingle(byte[] value, int startIndex) {
            var val = ToInt32(value, startIndex);
            return *(float*)&val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe short ToInt16(byte[] value, int startIndex) {
            fixed (byte* pbyte = &value[startIndex]) {
                if (startIndex % 2 == 0) return *(short*)pbyte;
                return BitConverter.IsLittleEndian ? (short)(*pbyte | (*(pbyte + 1) << 8)) : (short)((*pbyte << 8) | *(pbyte + 1));
            }
        }
        #endregion

        public int ReadInt32() {
            var pos = GetPosAndMove(4);
            return ToInt32(_buffer, pos);
        }

        public bool ReadBoolean() {
            return _buffer[GetPosAndMove(1)] != 0;
        }

        public uint ReadUInt32() {
            return (uint)ReadInt32();
        }

        public float ReadSingle() {
            return ToSingle(_buffer, GetPosAndMove(4));
        }

        public ushort ReadUInt16() {
            return (ushort)ToInt16(_buffer, GetPosAndMove(2));
        }

        public void Skip(int count) {
            if (_left >= count) {
                GetPosAndMove(count);
            } else {
                _stream.Seek(Position + count, SeekOrigin.Begin);
                _left = 0;
            }
        }

        public byte[] ReadBytes(int count) {
            var result = new byte[count];

            if (_left >= count) {
                Array.Copy(_buffer, GetPosAndMove(count), result, 0, count);
            } else {
                Array.Copy(_buffer, _total - _left, result, 0, _left);
                var read = _stream.Read(result, _left, count - _left);
                if (read != count - _left) throw new Exception("Unexpected end.");
                
                _left = 0;
            }

            return result;
        }

        public void Dispose() {
            _stream.Dispose();
        }

        public void Seek(long offset, SeekOrigin seekOrigin) {
            _left = 0;

            switch (seekOrigin) {
                case SeekOrigin.Begin:
                case SeekOrigin.End:
                    _stream.Seek(offset, seekOrigin);
                    break;
                case SeekOrigin.Current:
                    _stream.Seek(Position + offset, SeekOrigin.Begin);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seekOrigin), seekOrigin, null);
            }
        }

        #region Common stuff
        public float[] ReadSingle2D() {
            var pos = GetPosAndMove(8);
            return new[] {
                ToSingle(_buffer, pos),
                ToSingle(_buffer, pos + 4)
            };
        }

        public float[] ReadSingle3D() {
            var pos = GetPosAndMove(12);
            return new[] {
                ToSingle(_buffer, pos),
                ToSingle(_buffer, pos + 4),
                ToSingle(_buffer, pos + 8)
            };
        }

        public float[] ReadSingle4D() {
            var pos = GetPosAndMove(16);
            return new[] {
                ToSingle(_buffer, pos),
                ToSingle(_buffer, pos + 4),
                ToSingle(_buffer, pos + 8),
                ToSingle(_buffer, pos + 12)
            };
        }
        #endregion
    }

    internal class UsualBinaryReader : BinaryReader {
        public UsualBinaryReader([NotNull] string filename) : base(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public UsualBinaryReader([NotNull] Stream input) : base(input) { }

        public void Skip(int i) {
            BaseStream.Seek(i, SeekOrigin.Current);
        }

        public override string ReadString() {
            var length = ReadInt32();
            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        #region Common stuff
        public float[] ReadSingle2D() {
            return new[] {
                ReadSingle(), ReadSingle()
            };
        }

        public float[] ReadSingle3D() {
            return new[] {
                ReadSingle(), ReadSingle(), ReadSingle()
            };
        }

        public float[] ReadSingle4D() {
            return new[] {
                ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle()
            };
        }
        #endregion
    }
}