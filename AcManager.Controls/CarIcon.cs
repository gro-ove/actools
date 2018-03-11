using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class CarIcon : BetterImage {
        static CarIcon() {
            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update += OnUpdate;
        }

        private static void OnUpdate(object sender, EventArgs args) {
            Cache.Clear();
            if (!SettingsHolder.Content.MarkKunosContent) return;
            foreach (var image in VisualTreeHelperEx.GetAllOfType<CarIcon>()) {
                image.OnCarChanged(image.Car);
            }
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(CarIcon), new PropertyMetadata(OnCarChanged));

        public CarObject Car {
            get => (CarObject)GetValue(CarProperty);
            set => SetValue(CarProperty, value);
        }

        private static void OnCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CarIcon)o).OnCarChanged((CarObject)e.NewValue);
        }

        private static readonly Dictionary<string, BitmapEntry> Cache = new Dictionary<string, BitmapEntry>();

        private async void OnCarChanged(CarObject newValue) {
            var brand = newValue.Brand;
            if (brand == null) {
                OnFilenameChanged(newValue.BrandBadge);
                return;
            }

            var decodeWidth = InnerDecodeWidth;
            var key = $@"{brand.ToLowerInvariant()}:{decodeWidth}";

            if (!Cache.TryGetValue(key, out var entry)) {
                Cache[key] = entry = await LoadEntry(brand, decodeWidth, newValue.BrandBadge);
                if (Car != newValue) return;
            }

            SetCurrent(entry, null);
        }

        private static readonly TaskCache TaskCache = new TaskCache();

        private static Task<BitmapEntry> LoadEntry([NotNull] string key, int decodeWidth, string fallbackFilename) {
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $@"{key}.png");
                string filename;
                if (badge.Exists) {
                    filename = badge.Filename;
                } else {
#if DEBUG
                    Logging.Warning("Not found: " + badge.Filename);
#endif
                    if (!File.Exists(fallbackFilename)) return BitmapEntry.Empty;
                    filename = fallbackFilename;
                }
                return LoadBitmapSourceFromBytes(File.ReadAllBytes(filename), decodeWidth);
            }), key);
        }
    }
}