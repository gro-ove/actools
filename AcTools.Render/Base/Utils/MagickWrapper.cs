using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using ImageMagick;

namespace AcTools.Render.Base.Utils {
    public static class MagickWrapper {
        private static bool? _isSupported;
        public static bool IsSupported = _isSupported ?? (_isSupported = CheckIfSupported()).Value;

        private static bool CheckIfSupported() {
            try {
                CheckIfSupported_Inner();
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private static void CheckIfSupported_Inner() {
            var version = MagickNET.Version;
            if (version == null) {
                throw new Exception();
            }
        }

        public static void ResetIsSupported() {
            _isSupported = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static Image LoadFromFileAsImage(string filename) {
            using (var image = new MagickImage(filename)) {
                return image.ToBitmap();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadFromFileAsSlimDxBuffer(string filename) {
            using (var image = new MagickImage(filename)) {
                return image.ToByteArray(MagickFormat.Bmp);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadAsSlimDxBuffer(byte[] data) {
            using (var image = new MagickImage(data)) {
                return image.ToByteArray(MagickFormat.Bmp);
            }
        }
    }
}