using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;

namespace AcTools {
    /// <summary>
    /// Optimized version of BinaryReader — reads stuff to internal buffer, caches everything,
    /// works in ACSII encoding (todo: fix), has minified amount of different checks. Also, has
    /// some additional methods for skipping and seeking, which, if possible, won’t call Seek()
    /// of underlying Stream at all and would only change cursor in cached data instead.
    /// 
    /// Mostly, was made for Kunos binary files.
    /// </summary>
    public class ReadAheadBinaryReader : IDisposable {
        public static readonly bool IsLittleEndian = true;

        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _left, _total;
        private long? _length;

        public long Position {
            get { return _stream.Position - _left; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public long Length => _length ?? (_length = _stream.Length).Value;

        private class InnerStream : Stream {
            private readonly ReadAheadBinaryReader _parent;

            public InnerStream(ReadAheadBinaryReader parent) {
                _parent = parent;
            }

            public override void Flush() {}

            public override long Seek(long offset, SeekOrigin origin) {
                return _parent.Seek(offset, origin);
            }

            public override void SetLength(long value) {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                return _parent.ReadBytes(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => _parent.Length;

            public override long Position {
                get { return _parent.Position; }
                set { _parent.Seek(value, SeekOrigin.Begin); }
            }
        }

        public Stream BaseStream => _baseSteam ?? (_baseSteam = new InnerStream(this));
        private Stream _baseSteam;

        public ReadAheadBinaryReader(string filename, int bufferSize = 2048) {
            _stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8196);
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

                if (_left < count) throw new Exception("Unexpected end");
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

        public void SkipString() {
            Skip(ReadInt32());
        }

        #region Some convertation stuff
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe int ToInt32(byte[] value, int startIndex) {
            fixed (byte* pbyte = &value[startIndex]) {
                if (startIndex % 4 == 0) return *(int*)pbyte;
                return BitConverter.IsLittleEndian
                        ? *pbyte | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24)
                        : (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | *(pbyte + 3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe long ToInt64(byte[] value, int startIndex) {
            fixed (byte* numPtr = &value[startIndex]) {
                if (startIndex % 8 == 0) return *(long*)numPtr;
                if (BitConverter.IsLittleEndian) {
                    return (uint)(*numPtr | numPtr[1] << 8 | numPtr[2] << 16 | numPtr[3] << 24) |
                            (long)(numPtr[4] | numPtr[5] << 8 | numPtr[6] << 16 | numPtr[7] << 24) << 32;
                }

                int num = *numPtr << 24 | numPtr[1] << 16 | numPtr[2] << 8 | numPtr[3];
                return (uint)(numPtr[4] << 24 | numPtr[5] << 16 | numPtr[6] << 8) | numPtr[7] | (long)num << 32;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe float ToSingle(byte[] value, int startIndex) {
            var val = ToInt32(value, startIndex);
            return *(float*)&val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe short ToInt16(byte[] value, int startIndex) {
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

        public long ReadInt64() {
            var pos = GetPosAndMove(8);
            return ToInt64(_buffer, pos);
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
            ReadBytes(result, 0, count);
            return result;
        }

        public int ReadBytes(byte[] destination, int offset, int count) {
            if (_left >= count) {
                Array.Copy(_buffer, GetPosAndMove(count), destination, offset, count);
            } else {
                Array.Copy(_buffer, _total - _left, destination, offset, _left);
                offset += _left;
                count -= _left;

                var read = _stream.Read(destination, offset, count);
                if (read != count) throw new Exception("Unexpected end");
                
                _left = 0;
            }

            return count;
        }

        public void Dispose() {
            _stream.Dispose();
        }

        public long Seek(long offset, SeekOrigin seekOrigin) {
            var current = Position;
            long target;

            switch (seekOrigin) {
                case SeekOrigin.Begin:
                    target = offset;
                    break;
                case SeekOrigin.End:
                    target = Length + offset;
                    break;
                case SeekOrigin.Current:
                    target = current + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seekOrigin), seekOrigin, null);
            }

            var delta = target - current;
            if (delta == 0) return current;

            if (delta > 0) {
                if (delta <= _left) {
                    _left -= (int)delta;
                    return target;
                }
            } else {
                var leftBackwards = _total - _left;
                if (-delta <= leftBackwards) {
                    _left -= (int)delta;
                    return target;
                }
            }

            _left = 0;
            return _stream.Seek(target, SeekOrigin.Begin);
        }

        #region Common stuff
        public float[] ReadSingle2D() {
            var pos = GetPosAndMove(8);
            var buffer = _buffer;
            return new[] {
                ToSingle(buffer, pos),
                ToSingle(buffer, pos + 4)
            };
        }

        public float[] ReadSingle3D() {
            var pos = GetPosAndMove(12);
            var buffer = _buffer;
            return new[] {
                ToSingle(buffer, pos),
                ToSingle(buffer, pos + 4),
                ToSingle(buffer, pos + 8)
            };
        }

        public float[] ReadSingle4D() {
            var pos = GetPosAndMove(16);
            var buffer = _buffer;
            return new[] {
                ToSingle(buffer, pos),
                ToSingle(buffer, pos + 4),
                ToSingle(buffer, pos + 8),
                ToSingle(buffer, pos + 12)
            };
        }

        /// <summary>
        /// Read 64 bytes as 16 floats.
        /// </summary>
        /// <returns></returns>
        public float[] ReadMatrix() {
            var pos = GetPosAndMove(64);
            var buffer = _buffer;
            return new[] {
                ToSingle(buffer, pos), ToSingle(buffer, pos + 4), ToSingle(buffer, pos + 8), ToSingle(buffer, pos + 12),
                ToSingle(buffer, pos + 16), ToSingle(buffer, pos + 20), ToSingle(buffer, pos + 24), ToSingle(buffer, pos + 28),
                ToSingle(buffer, pos + 32), ToSingle(buffer, pos + 36), ToSingle(buffer, pos + 40), ToSingle(buffer, pos + 44),
                ToSingle(buffer, pos + 48), ToSingle(buffer, pos + 52), ToSingle(buffer, pos + 56), ToSingle(buffer, pos + 60)
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

        public void Seek(long offset, SeekOrigin origin) {
            BaseStream.Seek(offset, origin);
        }

        public long Position {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }
    }
}