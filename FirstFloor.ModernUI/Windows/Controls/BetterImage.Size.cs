using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public partial class BetterImage {
        private static byte[] _jpegSignature = { 0xFF, 0xD8, 0xFF };
        private static byte[] _pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static byte[] _icoSignature = { 0x00, 0x00, 0x01, 0x00 };

        public static Size? GetImageSize(MemoryStream stream, [Localizable(false)] string sourceDebug) {
            int index = 0, limit = Math.Min((int)stream.Length, 4096);
            if (limit == 0) return null;

            var data = new byte[limit];
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(data, 0, limit);

            try {
                if (NextAre(_jpegSignature)) {
                    // JPEG
                    while (index < limit && SkipUntil(0xFF) && SkipWhile(0xFF)) {
                        if (AnyOf(0xC0, 0xC2)) {
                            index += 3;
                            var height = NextShort();
                            var size = new Size(NextShort(), height);
                            return index >= limit ? (Size?)null : size;
                        }
                    }
                } else if (NextAre(_pngSignature)) {
                    // PNG
                    index += 16;
                    var size = new Size(NextInt(), NextInt());
                    return index >= limit ? (Size?)null : size;
                } else if (NextAre(_icoSignature)) {
                    // ICO
                    index += 4;
                    var result = new Size();
                    for (var i = NextShort(); i > 0 && index < limit; i--, index += 14) {
                        var size = new Size(Next(), Next());
                        if (size.Width > result.Width || size.Height > result.Height) {
                            result = size;
                        }
                    }
                    return result;
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }

#if DEBUG
            Logging.Warning($"Failed to determine size ({sourceDebug ?? @"?"}): {data}");
#endif

            return null;

            byte Next() => index >= limit ? (byte)0 : data[index++];
            int NextShort() => (Next() << 8) + Next();
            int NextInt() => (Next() << 24) + (Next() << 16) + (Next() << 8) + Next();

            bool NextAre(byte[] v) {
                if (index + v.Length >= limit) return false;
                for (var i = v.Length - 1; i >= 0; i--) {
                    if (data[index + i] != v[i]) return false;
                }
                return true;
            }

            bool SkipUntil(byte v) {
                index = (index >= limit ? -1 : Array.IndexOf(data, v, index)) + 1;
                return index > 0 && index < limit;
            }

            bool SkipWhile(byte v) {
                for (; index < limit && data[index] == v; index++) { }
                return index < limit;
            }

            bool AnyOf(byte b0, byte b1) {
                var n = Next();
                return b0 == n || b1 == n;
            }
        }
    }
}