using System.Drawing;
using ImageMagick;

namespace AcTools.Kn5Render.Utils {
    public static class MagickWrapper {
        public static Image LoadFromFileAsImage(string filename) {
            using (var image = new MagickImage(filename)) {
                return image.ToBitmap();
            }
        }

        public static byte[] LoadFromFileAsSlimDxBuffer(string filename) {
            using (var image = new MagickImage(filename)) {
                return image.ToByteArray(MagickFormat.Bmp);
            }
        }

        public static byte[] LoadFromBytesAsSlimDxBuffer(byte[] filename) {
            using (var image = new MagickImage(filename)) {
                return image.ToByteArray(MagickFormat.Bmp);
            }
        }
    }
}
