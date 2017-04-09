namespace AcTools {
    public static class CommonAcConsts {
        public static readonly string AppId = "244210";

        public static readonly int Kn5ActualVersion = 5;
        public static readonly int KsAnimActualVersion = 2;

        public static readonly int DriverWeight = 75;

        public static readonly int PreviewWidth = 1022;
        public static readonly int PreviewHeight = 575;

        public static readonly int TrackPreviewHeight = 200;
        public static readonly int TrackPreviewWidth = 355;

        public static readonly int TrackOutlineHeight = 192;
        public static readonly int TrackOutlineWidth = 365;
        
        /// <summary>
        /// Seconds from 00:00.
        /// </summary>
        public static readonly int TimeMinimum = 8 * 60 * 60;

        /// <summary>
        /// Seconds from 00:00.
        /// </summary>
        public static readonly int TimeMaximum = 18 * 60 * 60;

        public static readonly double TemperatureMinimum = 0d;
        public static readonly double TemperatureMaximum = 36d;

        public static readonly double RoadTemperatureMinimum = -15d;
        public static readonly double RoadTemperatureMaximum = 15d;
    }
}