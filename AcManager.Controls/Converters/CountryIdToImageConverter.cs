using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AcManager.Controls.Properties;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(string), typeof(BitmapImage))]
    public class CountryIdToImageConverter : IValueConverter {
        public static readonly CountryIdToImageConverter Instance = new CountryIdToImageConverter();

        private static ZipArchive _archive;

        private static readonly Dictionary<string, BitmapImage> Cache = new Dictionary<string, BitmapImage>(20);

        [CanBeNull]
        public BitmapImage Convert([CanBeNull] string id) {
            if (id == null) id = @"_";

            BitmapImage bi;
            if (Cache.TryGetValue(id, out bi)) return bi;

            if (_archive == null) {
                _archive = new ZipArchive(new MemoryStream(BinaryResources.Flags));
            }

            var entryStream = (_archive.GetEntry(id) ?? _archive.GetEntry(@"_"))?.Open();
            if (entryStream == null) {
                return null;
            }

            bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = entryStream.ReadAsMemoryStream();
            bi.EndInit();
            bi.Freeze();

            Cache[id] = bi;
            return bi;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var id = value?.ToString();
            return Convert(id?.Length != 2 ? null : id);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
