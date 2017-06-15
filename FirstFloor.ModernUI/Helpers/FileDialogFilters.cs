namespace FirstFloor.ModernUI.Helpers {
    public static class FileDialogFilters {
        public static string ImagesFilter => UiStrings.ImagesFilter;
        public static string TexturesFilter => UiStrings.TexturesFilter;
        public static string TextFilter => "Text Files|*.txt|All files (*.*)|*.*";
        public static string LutFilter => "LUT Tables|*.lut|All files (*.*)|*.*";
        public static string CsvFilter => "CSV Tables|*.csv|All files (*.*)|*.*";
    }
}
