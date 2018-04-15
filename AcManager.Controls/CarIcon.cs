using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
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

        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();

        private async void OnCarChanged(CarObject newValue) {
            var brand = newValue.Brand;
            if (brand == null) {
                OnFilenameChanged(newValue.BrandBadge);
                return;
            }

            var icon = await LoadEntryAsync(brand, InnerDecodeWidth, newValue.BrandBadge);
            if (Car == newValue) {
                SetCurrent(icon, null);
            }
        }

        private static readonly TaskCache TaskCache = new TaskCache();

        private static Task<Image> LoadEntryAsyncInner([CanBeNull] string key, int decodeWidth, string fallbackFilename) {
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $@"{key}.png");
                string filename;
                if (badge.Exists) {
                    filename = badge.Filename;
                } else {
                    if (!File.Exists(fallbackFilename)) return Image.Empty;
                    filename = fallbackFilename;
                }
                return LoadBitmapSourceFromBytes(File.ReadAllBytes(filename), decodeWidth);
            }), key);
        }

        public static Image GetCached(string brandName, int decodeWidth) {
            var key = $@"{brandName?.ToLowerInvariant()}:{decodeWidth}";
            lock (Cache) {
                return Cache.TryGetValue(key, out var entry) ? entry : Image.Empty;
            }
        }

        public static async Task<Image> LoadEntryAsync([CanBeNull] string brandName, int decodeWidth, string fallbackFilename) {
            var key = $@"{brandName?.ToLowerInvariant()}:{decodeWidth}";

            bool got;
            Image entry;

            lock (Cache) {
                got = Cache.TryGetValue(key, out entry);
            }

            if (!got) {
                entry = await LoadEntryAsyncInner(brandName, decodeWidth, fallbackFilename).ConfigureAwait(false);
                lock (Cache) {
                    Cache[key] = entry;
                }
            }

            return entry;
        }
    }
}