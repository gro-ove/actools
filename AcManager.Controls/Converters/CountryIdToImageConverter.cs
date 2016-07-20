using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AcManager.Controls.Properties;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls.Converters {
    public class CountryIdToImageConverter
        : IValueConverter {
        private static ZipArchive _archive;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var id = value?.ToString();
            if (id?.Length != 2) {
                id = @"_";
            }

            if (_archive == null) {
                _archive = new ZipArchive(new MemoryStream(BinaryResources.Flags));
            }
            
            var entryStream = (_archive.GetEntry(id) ?? _archive.GetEntry(@"_"))?.Open();
            if (entryStream == null) {
                return null;
            }

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = entryStream.ReadAsMemoryStream();
            bi.EndInit();
            return bi;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}
