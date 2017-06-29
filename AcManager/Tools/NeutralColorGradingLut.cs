using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;
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

            var dialog = new SaveFileDialog {
                Filter = FileDialogFilters.ImagesFilter,
                DefaultExt = ".png",
                FileName = "Neutral color grading.png"
            };

            if (dialog.ShowDialog() != true) {
                return;
            }

            bitmap.Save(dialog.FileName);
        }
    }
}