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
            var brand = newValue?.Brand;
            if (brand == null) {
                OnFilenameChanged(newValue?.BrandBadge);
                return;
            }

            var icon = await LoadEntryAsync(brand, InnerDecodeWidth, newValue.BrandBadge);
            if (Car == newValue) {
                SetCurrent(icon, null);
            }
        }

        private static readonly TaskCache TaskCache = new TaskCache();

        private static Task<Image> LoadEntryAsyncInner([CanBeNull] string brandName, int decodeWidth, string fallbackFilename) {
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, $@"{brandName}.png");
                string filename;
                if (badge.Exists) {
                    filename = badge.Filename;
                } else {
                    if (!File.Exists(fallbackFilename)) return Image.Empty;
                    filename = fallbackFilename;
                }
                var ret = LoadBitmapSourceFromBytes(File.ReadAllBytes(filename), decodeWidth);
                lock (Cache) {
                    Cache[$@"{brandName?.ToLowerInvariant()}:{decodeWidth}"] = ret;
                }
                return ret;
            }), brandName);
        }

        public static Image GetCached(string brandName, int decodeWidth) {
            var key = $@"{brandName?.ToLowerInvariant()}:{decodeWidth}";
            lock (Cache) {
                return Cache.TryGetValue(key, out var entry) ? entry : Image.Empty;
            }
        }

        public static Task<Image> LoadEntryAsync([CanBeNull] string brandName, int decodeWidth, string fallbackFilename) {
            bool got;
            Image entry;
            lock (Cache) {
                got = Cache.TryGetValue($@"{brandName?.ToLowerInvariant()}:{decodeWidth}", out entry);
            }
            return got ? Task.FromResult(entry) : LoadEntryAsyncInner(brandName, decodeWidth, fallbackFilename);
        }
    }
}