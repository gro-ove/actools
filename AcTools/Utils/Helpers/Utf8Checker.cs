using System;

namespace AcTools.Utils.Helpers {
    public static class Utf8Checker {
        public static bool IsUtf8(byte[] buffer, int limit = int.MaxValue) {
            var position = 0;
            var bytes = 0;
            var length = buffer.Length;
            while (position < length && position < limit) {
                if (buffer[position] > 0x7F) {
                    if (!IsValid(buffer, position, length, ref bytes)) {
                        return false;
                    }
                    position += bytes;
                } else {
                    position++;
                }
            }
            return true;
        }

        private static bool IsValid(byte[] buffer, int position, int length, ref int bytes) {
            if (length > buffer.Length) {
                throw new ArgumentException("Invalid length");
            }

            if (position > length - 1) {
                bytes = 0;
                return true;
            }

            var ch = buffer[position];

            if (ch <= 0x7F) {
                bytes = 1;
                return true;
            }

            if (ch >= 0xc2 && ch <= 0xdf) {
                if (position > length - 2) {
                    bytes = 0;
                    return false;
                }
                if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf) {
                    bytes = 0;
                    return false;
                }
                bytes = 2;
                return true;
            }

            if (ch == 0xe0) {
                if (position > length - 3) {
                    bytes = 0;
                    return false;
                }

                if (buffer[position + 1] < 0xa0 || buffer[position + 1] > 0xbf ||
                    buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf) {
                    bytes = 0;
                    return false;
                }
                bytes = 3;
                return true;
            }


            if (ch >= 0xe1 && ch <= 0xef) {
                if (position > length - 3) {
                    bytes = 0;
                    return false;
                }

                if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf ||
                    buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf) {
                    bytes = 0;
                    return false;
                }

                bytes = 3;
                return true;
            }

            if (ch == 0xf0) {
                if (position > length - 4) {
                    bytes = 0;
                    return false;
                }

                if (buffer[position + 1] < 0x90 || buffer[position + 1] > 0xbf ||
                    buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                    buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf) {
                    bytes = 0;
                    return false;
                }

                bytes = 4;
                return true;
            }

            if (ch == 0xf4) {
                if (position > length - 4) {
                    bytes = 0;
                    return false;
                }

                if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0x8f ||
                    buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                    buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf) {
                    bytes = 0;
                    return false;
                }

                bytes = 4;
                return true;
            }

            if (ch >= 0xf1 && ch <= 0xf3) {
                if (position > length - 4) {
                    bytes = 0;
                    return false;
                }

                if (buffer[position + 1] < 0x80 || buffer[position + 1] > 0xbf ||
                    buffer[position + 2] < 0x80 || buffer[position + 2] > 0xbf ||
                    buffer[position + 3] < 0x80 || buffer[position + 3] > 0xbf) {
                    bytes = 0;
                    return false;
                }

                bytes = 4;
                return true;
            }

            return false;
        }
    }
}