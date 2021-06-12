using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class FontObjectBitmap {
        public FontObjectBitmap(string bitmapFilename, string fontFilename) :
                this((BitmapSource)BetterImage.LoadBitmapSource(bitmapFilename).ImageSource, File.ReadAllBytes(fontFilename)) { }

        public FontObjectBitmap(byte[] bitmapData, byte[] fontData) :
                this((BitmapSource)BetterImage.LoadBitmapSourceFromBytes(bitmapData).ImageSource, fontData) { }

        private FontObjectBitmap(BitmapSource font, byte[] fontData) {
            _fontBitmapImage = font;

            try {
                _fontList = fontData.ToUtf8String().Split('\n').Select(line => FlexibleParser.TryParseDouble(line) ?? 0d).ToList();
            } catch (Exception e) {
                Logging.Warning("File damaged: " + e);
            }
        }

        [ItemCanBeNull]
        public static async Task<FontObjectBitmap> CreateAsync(string bitmapFilename, string fontFilename) {
            try {
                var image = (await BetterImage.LoadBitmapSourceAsync(bitmapFilename)).ImageSource as BitmapSource;
                var data = await FileUtils.ReadAllBytesAsync(fontFilename);
                return new FontObjectBitmap(image, data);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [CanBeNull]
        private readonly BitmapSource _fontBitmapImage;

        [CanBeNull]
        private readonly List<double> _fontList;

        private const char FirstChar = (char)32;
        private const char LastChar = (char)126;

        private static int CharToId(char c) => c < FirstChar || c > LastChar ? 0 : c - FirstChar;

        [CanBeNull]
        public CroppedBitmap BitmapForChar(char c) {
            if (_fontBitmapImage == null) {
                Logging.Warning("_fontBitmapImage == null!");
                return null;
            }

            if (_fontList == null) {
                Logging.Warning("_fontList == null!");
                return null;
            }

            var i = CharToId(c);
            var x = _fontList[i];
            var width = (i + 1 == _fontList.Count ? 1d : _fontList[i + 1]) - x;

            if (x + width <= 0d || x >= 1d) return null;
            if (x < 0) {
                width += x;
                x = 0d;
            }

            width = Math.Min(width, 1d - x);

            var rect = new Int32Rect((int)(x * _fontBitmapImage.PixelWidth), 0, (int)(width * _fontBitmapImage.PixelWidth), _fontBitmapImage.PixelHeight);
            var result = new CroppedBitmap(_fontBitmapImage, rect);
            result.Freeze();
            return result;
        }

        public CroppedBitmap GetIcon() {
            return BitmapForChar(SettingsHolder.Content.FontIconCharacter?.Cast<char?>().FirstOrDefault() ?? '3');
        }
    }
}