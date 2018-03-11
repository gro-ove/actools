using System;
using System.Collections.Generic;
using System.IO;
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
            var decodeWidth = InnerDecodeWidth;
            var key = $@"{newValue?.ToLowerInvariant()}:{decodeWidth}";
            if (!Cache.TryGetValue(key, out var entry)) {
                Cache[key] = entry = await LoadEntry(newValue, decodeWidth);
                if (Country != newValue) return;
            }
            SetCurrent(entry, null);
        }

        private static readonly Dictionary<string, BitmapEntry> Cache = new Dictionary<string, BitmapEntry>();

        private static readonly TaskCache TaskCache = new TaskCache();

        private static bool IsCountryId(string key) {
            if (key.Length == 2) {
                return char.IsUpper(key[0]) && char.IsUpper(key[1]);
            }

            if (key.Length == 2) {
                return char.IsUpper(key[0]) && char.IsUpper(key[1]) && key[2] == '-';
            }

            return false;
        }

        private static Task<BitmapEntry> LoadEntry([CanBeNull] string key, int decodeWidth) {
            key = (key == null ? null : IsCountryId(key) ? key : AcStringValues.GetCountryId(key)) ?? @"default";
            return TaskCache.Get(() => Task.Run(() => {
                var badge = FilesStorage.Instance.GetContentFile(ContentCategory.CountryFlags, $@"{key}.png");
                return badge.Exists ? LoadBitmapSourceFromBytes(File.ReadAllBytes(badge.Filename), decodeWidth) : BitmapEntry.Empty;
            }), key);
        }
    }
}