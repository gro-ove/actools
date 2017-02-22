using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(string), typeof(BitmapImage))]
    public class CountryIdToImageConverter : IValueConverter {
        private static string _baseDirectory, _overridesDirectory;

        public static void Initialize(string baseDirectory, string overridesDirectory) {
            _baseDirectory = baseDirectory;
            _overridesDirectory = overridesDirectory;
            ResetCache();
        }

        public static void ResetCache() {
            Cache.Clear();
        }

        private static readonly Dictionary<string, BitmapImage> Cache = new Dictionary<string, BitmapImage>(220);

        [CanBeNull]
        private static byte[] GetFlagBytes([CanBeNull] string countryId) {
            var name = $@"{countryId ?? @"default"}.png";
            if (_overridesDirectory != null) {
                var filename = Path.Combine(_overridesDirectory, name);
                if (File.Exists(filename)) return File.ReadAllBytes(filename);
            }

            if (_baseDirectory != null) {
                var filename = Path.Combine(_baseDirectory, name);
                if (File.Exists(filename)) return File.ReadAllBytes(filename);
            }

            return null;
        }

        [CanBeNull]
        public static BitmapImage Convert([CanBeNull] string id) {
            if (id == @"_") id = null; // just in case

            BitmapImage bi;
            if (Cache.TryGetValue(id ?? "", out bi)) return bi;

            var bytes = GetFlagBytes(id);
            
            if (bytes == null) {
                Cache[id ?? ""] = null;
                return null;
            }

            // WrappingSteam here helps to avoid memory leaks. For more information:
            // https://code.logos.com/blog/2008/04/memory_leak_with_bitmapimage_and_memorystream.html
            using (var memory = new MemoryStream(bytes))
            using (var stream = new WrappingStream(memory)) {
                bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = stream;
                bi.EndInit();
                bi.Freeze();

                Cache[id ?? ""] = bi;
                return bi;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return Convert(null);

            var id = value.ToString();
            if (id.Length < 2) return Convert(null);

            for (var i = 0; i < id.Length; i++) {
                if (char.IsLower(id[i])) return Convert(null);
            }

            return Convert(id);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
