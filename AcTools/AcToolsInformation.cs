namespace AcTools {
    public static class AcToolsInformation {
        public static string Name => $"AcTools {typeof(AcToolsInformation).Assembly.GetName().Version}";
    }

    public static class CommonAcConsts {
        public const int Kn5ActualVersion = 5;
        public const int DriverWeight = 75;

        public const int PreviewWidth = 1022;
        public const int PreviewHeight = 575;

        public const int TrackPreviewWidth = 200;
        public const int TrackPreviewHeight = 355;
    }
}
