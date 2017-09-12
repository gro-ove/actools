using System;

namespace FirstFloor.ModernUI.Helpers {
    // [Obsolete]
    public static class FileDialogFilters {
        public static string ImagesFilter => UiStrings.ImagesFilter;
        public static string TexturesFilter => UiStrings.TexturesFilter;
        public static string TexturesAllFilter => "Image Files|*.dds;*.tif;*.tiff;*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
        public static string TexturesDdsFilter => UiStrings.TexturesDdsFilter;
        public static string ApplicationsFilter => "Applications (*.exe)|*.exe|All files (*.*)|*.*";
        public static string ZipFilter => "ZIP Archives (*.zip)|*.zip|All files (*.*)|*.*";
        public static string TarGzFilter => "Tar GZip Archives (*.tar.gz)|*.tar.gz|All files (*.*)|*.*";
        public static string ArchivesFilter => "Archives|*.zip;*.rar;*.7z;*.gzip;*.tar;*.tar.gz;*.bz2|All files (*.*)|*.*";
        public static string TextFilter => "Text Files (*.txt)|*.txt|All files (*.*)|*.*";
        public static string LutFilter => "LUT Tables (*.lut)|*.lut|All files (*.*)|*.*";
        public static string CsvFilter => "CSV Tables (*.csv)|*.csv|All files (*.*)|*.*";
    }
}
