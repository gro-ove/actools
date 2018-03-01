using System;
using System.Drawing;
using System.IO;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public partial class BetterImage {
        #region Loading
        public static Size? GetImageSize(byte[] data) {
            int index = 0, limit = Math.Min(data.Length, 4000);
            if (limit == 0) return null;

            try {
                if (NextAre(0xFF, 0xD8, 0xFF)) {
                    // JPEG
                    while (index < limit) {
                        if (SkipUntil(0xFF) && SkipWhile(0xFF) && AnyOf(0xC0, 0xC2)) {
                            index += 3;
                            var height = NextShort();
                            var size = new Size(NextShort(), height);
                            return index >= limit ? (Size?)null : size;
                        }
                    }
                } else if (NextAre(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)) {
                    // PNG
                    index += 16;
                    var size = new Size(NextInt(), NextInt());
                    return index >= limit ? (Size?)null : size;
                } else if (NextAre(0x00, 0x00, 0x01, 0x00)) {
                    // ICO
                    var result = new Size();
                    for (var i = NextShort(); i > 0 && index < limit; i--, index += 14) {
                        var size = new Size(Next(), Next());
                        if (size.Width > result.Width || size.Height > result.Height) {
                            result = size;
                        }
                    }
#if DEBUG
                    Logging.Debug(result);
#endif
                    return result;
                }
            } catch (Exception e) {
                Logging.Warning(e.Message);
            }

#if DEBUG
            Logging.Warning("Failed to determine size: " + data);

            // TODO: Remove me!
            if (Directory.Exists(@"U:\test")) {
                File.WriteAllBytes(@"U:\test\failed.bin", data);
            }
#endif

            return null;

            byte Next() => index >= limit ? (byte)0 : data[index++];
            int NextShort() => (Next() << 8) + Next();
            int NextInt() => (Next() << 24) + (Next() << 16) + (Next() << 8) + Next();

            bool NextAre(params byte[] v) {
                if (index + v.Length >= data.Length) return false;
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

            bool AnyOf(params byte[] v) {
                return Array.IndexOf(v, Next()) != -1;
            }
        }
        #endregion
    }
}