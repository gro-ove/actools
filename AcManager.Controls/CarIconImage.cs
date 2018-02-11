using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    public class CarIconImage : BetterImage {
        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(CarIconImage), new PropertyMetadata(OnCarChanged));

        public CarObject Car {
            get => (CarObject)GetValue(CarProperty);
            set => SetValue(CarProperty, value);
        }

        private static void OnCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CarIconImage)o).OnCarChanged((CarObject)e.NewValue);
        }

        private static readonly Dictionary<string, BitmapEntry> Cache = new Dictionary<string, BitmapEntry>();

        private async void OnCarChanged(CarObject newValue) {
            var key = newValue.Brand;
            if (key == null) {
                Filename = newValue.BrandBadge;
                return;
            }

            var decodeWidth = DecodeWidth;
            key = $@"{key.ToLowerInvariant()}:{decodeWidth}";

            if (!Cache.TryGetValue(key, out var entry)) {
                Cache[key] = entry = await LoadEntry(key, decodeWidth, newValue.BrandBadge);
                if (Car != newValue) return;
            }

            SetCurrent(entry, null);
        }

        private static readonly TaskCache TaskCache = new TaskCache();

        private static Task<BitmapEntry> LoadEntry(string key, int decodeWidth, string fallbackFilename) {
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $@"{key}.png");
                string filename;
                if (badge.Exists) {
                    filename = badge.Filename;
                } else {
                    if (!File.Exists(fallbackFilename)) return BitmapEntry.Empty;
                    filename = fallbackFilename;
                }
                return LoadBitmapSourceFromBytes(File.ReadAllBytes(filename), decodeWidth);
            }), key);
        }
    }
}