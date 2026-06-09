using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public class CountryIcon : BetterImage {
        static CountryIcon() {
            FilesStorage.Instance.Watcher(ContentCategory.CountryFlags).Update += OnUpdate;
        }

        private static void OnUpdate(object sender, EventArgs args) {
            Cache.Clear();
            if (!SettingsHolder.Content.MarkKunosContent) return;
            foreach (var image in VisualTreeHelperEx.GetAllOfType<CountryIcon>()) {
                image.OnCountryChanged(image.Country);
            }
        }

        public static readonly DependencyProperty CountryProperty = DependencyProperty.Register(nameof(Country), typeof(string),
                typeof(CountryIcon), new PropertyMetadata(OnCountryChanged));

        public string Country {
            get => (string)GetValue(CountryProperty);
            set => SetValue(CountryProperty, value);
        }

        private static void OnCountryChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CountryIcon)o).OnCountryChanged((string)e.NewValue);
        }

        private async void OnCountryChanged(string newValue) {
            var icon = await LoadEntryAsync(newValue, InnerDecodeWidth);
            if (Country == newValue) {
                SetCurrent(icon, null);
            }
        }

        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();

        private static readonly TaskCache TaskCache = new TaskCache();

        private static bool IsCountryId(string key) {
            return (key.Length == 2 || key.Length == 6 && key[2] == '-') && char.IsUpper(key[0]) && char.IsUpper(key[1]);
        }

        private static Task<Image> LoadEntryAsyncInner([CanBeNull] string key, int decodeWidth) {
            key = (key == null ? null : IsCountryId(key) ? key : AcStringValues.GetCountryId(key)) ?? @"default";
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.CountryFlags, $@"{key}.png");
                return badge.Exists ? LoadBitmapSourceFromFilename(badge.Filename, decodeWidth) : Image.Empty;
            }), key);
        }

        public static async Task<Image> LoadEntryAsync([CanBeNull] string countryId, int decodeWidth) {
            var key = $@"{countryId?.ToLowerInvariant()}:{decodeWidth}";

            bool got;
            Image entry;

            lock (Cache) {
                got = Cache.TryGetValue(key, out entry);
            }

            if (!got) {
                entry = await LoadEntryAsyncInner(countryId, decodeWidth).ConfigureAwait(false);
                lock (Cache) {
                    Cache[key] = entry;
                }
            }

            return entry;
        }
    }
}