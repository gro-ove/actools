using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FirstFloor.ModernUI.Dialogs;
using Bitmap = System.Drawing.Bitmap;

namespace AcManager.Tools {
    public static class NeutralColorGradingLut {
        public static void CreateNeutralLut(int size) {
            var width = size * size;
            var bitmap = new Bitmap(width, size, PixelFormat.Format24bppRgb);
            var bits = bitmap.LockBits(new Rectangle(0, 0, width, size), ImageLockMode.WriteOnly, bitmap.PixelFormat);

            var ptr = bits.Scan0;
            var row = bits.Stride * bitmap.Height;
            var data = new byte[row];

            var m = 255d / (size - 1);
            for (var g = 0; g < size; g++) {
                var o = g * width * 3;

                var b = 0;
                var r = 0;
                for (var i = 0; i < width; i++) {
                    var u = o + i * 3;

                    data[u] = (byte)(r * m);
                    data[u + 1] = (byte)(g * m);
                    data[u + 2] = (byte)(b * m);

                    if (++b == size) {
                        b = 0;
                        r++;
                    }
                }
            }

            Marshal.Copy(data, 0, ptr, row);
            bitmap.UnlockBits(bits);

            var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                Filters = { DialogFilterPiece.ImageFiles },
                DetaultExtension = ".png",
                DirectorySaveKey = "neutralColorLut",
                DefaultFileName = "Neutral color grading.png"
            });
            if (filename == null) return;
            bitmap.Save(filename);
        }
    }
}