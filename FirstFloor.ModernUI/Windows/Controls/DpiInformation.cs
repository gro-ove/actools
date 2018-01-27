namespace FirstFloor.ModernUI.Windows.Controls {
    public class DpiInformation {
        // Largest scale recorded, to decode images in a proper resulution
        public static double MaxScaleX { get; private set; }
        public static double MaxScaleY { get; private set; }

        internal DpiInformation(double wpfDpiX, double wpfDpiY) {
            WpfDpiX = wpfDpiX;
            WpfDpiY = wpfDpiY;
        }

        public double WpfDpiX { get; }
        public double WpfDpiY { get; }

        public double? MonitorDpiX { get; private set; }
        public double? MonitorDpiY { get; private set; }

        public double UserScale { get; private set; } = 1d;
        public double ScaleX { get; private set; } = 1d;
        public double ScaleY { get; private set; } = 1d;
        public bool IsScaled => ScaleX != 1d || ScaleY != 1d;

        private void UpdateScale() {
            ScaleX = UserScale * (MonitorDpiX ?? WpfDpiX) / WpfDpiX;
            ScaleY = UserScale * (MonitorDpiY ?? WpfDpiY) / WpfDpiY;
            if (ScaleX > MaxScaleX) MaxScaleX = ScaleX;
            if (ScaleY > MaxScaleY) MaxScaleY = ScaleY;
        }

        public bool UpdateUserScale(double scale) {
            if (UserScale == scale) return false;
            UserScale = scale;
            UpdateScale();
            return true;
        }

        internal bool UpdateMonitorDpi(double dpiX, double dpiY) {
            if (MonitorDpiX == dpiX || MonitorDpiY == dpiY) return false;
            MonitorDpiX = dpiX;
            MonitorDpiY = dpiY;
            UpdateScale();
            return true;
        }
    }
}