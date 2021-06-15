// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.IO;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    public class FbxAsciiFileInfo {
        public int Line { get; private set; }

        public int Column { get; private set; }

        private const int bufferLength = 65535;

        private char _peekedChar;
        private int _bufferSize;
        private int _bufferPos;

        private readonly byte[] _buffer;
        private readonly Stream _stream;
        private readonly long _streamLength;

        private char GetChar() {
            if (_bufferPos == _bufferSize) {
                _bufferSize = _stream.Read(_buffer, 0, bufferLength);
                _bufferPos = 0;
            }
            _peekedChar = (char)_buffer[_bufferPos];
            _bufferPos++;
            return _peekedChar;
        }

        public char PeekChar() {
            if (_peekedChar != char.MinValue) {
                return _peekedChar;
            }
            return GetChar();
        }

        public char ReadChar() {
            char c;
            if (_peekedChar != char.MinValue) {
                c = _peekedChar;
                _peekedChar = char.MinValue;
                return c;
            }
            c = GetChar();

            if (c.IsLineFeed()) {
                Column = 0;
                Line++;
            }

            Column++;
            return c;
        }

        public bool IsEndOfStream() {
            return _stream.Position == _streamLength && _bufferPos == _bufferSize;
        }

        public FbxAsciiFileInfo(Stream stream) {
            Line = 1;
            Column = 0;

            _stream = stream;
            _streamLength = _stream.Length;
            _peekedChar = char.MinValue;
            _buffer = new byte[bufferLength];
            _bufferSize = 0;
            _bufferPos = 0;
        }
    }
}