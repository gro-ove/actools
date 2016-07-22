using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Media {
    public static class BrokenIcon {
        private static BitmapSource _imageSource;

        public static BitmapSource ImageSource => _imageSource ?? (_imageSource = Load());

        public static int Width => 14;

        public static int Height => 16;

        public static Size Size { get; } = new Size(14, 16);

        private static BitmapSource Load() {
            using (var stream = new WrappingStream(new MemoryStream(BinaryResources.BrokenImage))) {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = stream;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }
    }
}