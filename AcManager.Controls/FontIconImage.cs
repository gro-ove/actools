using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    public class FontIconImage : BetterImage {
        public FontIconImage() {
            ShowBroken = false;
        }

        protected override unsafe bool FindCropArea(BitmapSource b, out int left, out int top, out int right, out int bottom) {
            var channels = b.Format.Masks.Count;
            if (channels != 3 && channels != 4) {
                left = top = right = bottom = 0;
                return true;
            }

            int w = b.PixelWidth, h = b.PixelHeight, s = (w * b.Format.BitsPerPixel + 7) / 8;
            var a = new byte[h * s];
            b.CopyPixels(a, s, 0);

            var alpha = channels == 4 ? b.Format.Masks.ElementAtOrDefault(0).Mask : null;
            var red = b.Format.Masks.ElementAtOrDefault(channels == 4 ? 1 : 0).Mask;
            if (red == null) {
                left = top = right = bottom = 0;
                return true;
            }

            var m1 = alpha == null ? 0U : BitConverter.ToUInt32(alpha.Reverse().ToArray(), 0);
            var m2 = BitConverter.ToUInt32(red.Reverse().ToArray(), 0);

            left = w;
            top = h;
            bottom = right = 0;

            fixed (byte* p = a) {
                var u = (uint*)p;
                var o = 0;
                for (var y = 0; y < h; y++) {
                    for (var x = 0; x < w; x++) {
                        if ((u[o] & m1) != 0 && (u[o] & m2) != 0) {
                            if (x < left) left = x;
                            if (y < top) top = y;
                            if (x > right) right = x;
                            if (y > bottom) bottom = y;
                        }
                        o++;
                    }
                }
            }
            return false;
        }

        static FontIconImage() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FontIconImage), new FrameworkPropertyMetadata(typeof(FontIconImage)));
        }
    }
}