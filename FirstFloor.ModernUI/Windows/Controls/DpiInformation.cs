using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Provides DPI information for a window.
    /// </summary>
    public class DpiInformation {
        internal DpiInformation(double wpfDpiX, double wpfDpiY) {
            WpfDpiX = wpfDpiX;
            WpfDpiY = wpfDpiY;
            ScaleX = 1;
            ScaleY = 1;
        }

        /// <summary>
        /// Gets the horizontal resolution of the WPF rendering DPI.
        /// </summary>
        public double WpfDpiX { get; }

        /// <summary>
        /// Gets the vertical resolution of the WPF rendering DPI.
        /// </summary>
        public double WpfDpiY { get; }

        /// <summary>
        /// Gets the horizontal resolution of the current monitor DPI.
        /// </summary>
        /// <remarks>Null when the process is not per monitor DPI aware.</remarks>
        public double? MonitorDpiX { get; private set; }

        /// <summary>
        /// Gets the vertical resolution of the current monitor DPI.
        /// </summary>
        /// <remarks>Null when the process is not per monitor DPI aware.</remarks>
        public double? MonitorDpiY { get; private set; }

        /// <summary>
        /// Gets the x-axis scale factor.
        /// </summary>
        public double ScaleX { get; private set; }

        /// <summary>
        /// Gets the y-axis scale factor.
        /// </summary>
        public double ScaleY { get; private set; }

        internal Vector UpdateMonitorDpi(double dpiX, double dpiY) {
            // calculate the vector of the current to new dpi
            var oldDpiX = MonitorDpiX ?? WpfDpiX;
            var oldDpiY = MonitorDpiY ?? WpfDpiY;

            MonitorDpiX = dpiX;
            MonitorDpiY = dpiY;

            ScaleX = dpiX / WpfDpiX;
            ScaleY = dpiY / WpfDpiY;

            return new Vector(dpiX / oldDpiX, dpiY / oldDpiY);
        }
    }
}
