using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public double DeviceScaleX { get; private set; } = 1d;
        public double DeviceScaleY { get; private set; } = 1d;

        public double DeviceLeft {
            get => Left * DeviceScaleX;
            set => Left = value / DeviceScaleX;
        }

        public double DeviceTop {
            get => Top * DeviceScaleY;
            set => Top = value / DeviceScaleY;
        }

        public double DeviceWidth {
            get => Width * DeviceScaleX;
            set => Width = value / DeviceScaleX;
        }

        public double DeviceHeight {
            get => Height * DeviceScaleY;
            set => Height = value / DeviceScaleY;
        }

        [ContractAnnotation("null => null; notnull => notnull")]
        private WpfScreen ToWpfScreen([CanBeNull] Screen screen) {
            return screen == null ? null : new WpfScreen(screen, DeviceScaleX, DeviceScaleY);
        }

        private System.Drawing.Point ToDevicePoint(Point devicePoint) {
            return new System.Drawing.Point((int)(devicePoint.X * DeviceScaleX), (int)(devicePoint.Y * DeviceScaleY));
        }

        private Point ToWpfPoint(System.Drawing.Point devicePoint) {
            return new Point(devicePoint.X / DeviceScaleX, devicePoint.Y / DeviceScaleY);
        }

        [NotNull]
        public WpfScreen GetScreen() {
            return ToWpfScreen(Screen.FromHandle(new WindowInteropHelper(this).Handle));
        }

        [NotNull]
        public WpfScreen ScreenFromPoint(Point wpfPoint) {
            return ToWpfScreen(Screen.FromPoint(new System.Drawing.Point((int)(wpfPoint.X * DeviceScaleX), (int)(wpfPoint.Y * DeviceScaleY))));
        }

        [NotNull]
        public WpfScreen[] AllScreens {
            get {
                var screens = Screen.AllScreens;
                var result = new WpfScreen[screens.Length];
                for (var i = 0; i < screens.Length; i++) {
                    result[i] = ToWpfScreen(screens[i]);
                }
                return result;
            }
        }

        public class WpfScreen {
            private readonly Screen _screen;
            private readonly double _scaleX;
            private readonly double _scaleY;

            public WpfScreen([NotNull] Screen screen, double scaleX, double scaleY) {
                _screen = screen;
                _scaleX = 1d / scaleX;
                _scaleY = 1d / scaleY;
            }

            public bool IsPrimary => _screen.Primary;
            public string DeviceName => _screen.DeviceName;

            public Rect Bounds => new Rect(
                    new Point(_screen.Bounds.Left * _scaleX, _screen.Bounds.Top * _scaleY),
                    new Size(_screen.Bounds.Width * _scaleX, _screen.Bounds.Height * _scaleY));

            public Rect WorkingArea => new Rect(
                    new Point(_screen.WorkingArea.Left * _scaleX, _screen.WorkingArea.Top * _scaleY),
                    new Size(_screen.WorkingArea.Width * _scaleX, _screen.WorkingArea.Height * _scaleY));

            public override string ToString() {
                return $@"{GetType().Name}[WorkingArea={WorkingArea}, Primary={IsPrimary}, DeviceName={DeviceName}]";
            }
        }

        [NotNull]
        public WpfScreen GetPreferredScreen(DpiAwareWindow screenFor = null) {
            return ToWpfScreen(GetPreferredDeviceScreen(screenFor));
        }

        [CanBeNull]
        public WpfScreen GetForcedScreen(DpiAwareWindow screenFor = null) {
            return ToWpfScreen(GetForcedDeviceScreen(screenFor));
        }

        [NotNull]
        private static Screen GetLastUsedDeviceScreen(DpiAwareWindow screenFor = null) {
            var window = screenFor?.Owner as DpiAwareWindow ?? LastActiveWindow;
            return LogResult(screenFor, !ReferenceEquals(window, screenFor) && window?.IsLoaded == true ? window.GetDeviceScreen() : Screen.FromPoint(
                    LogResult(screenFor, ValuesStorage.Get<System.Drawing.Point?>(DefaultScreenKey), "Saved screen point")
                            ?? LogResult(screenFor, Control.MousePosition, "Mouse at")));
        }

        [NotNull]
        public static Screen GetPreferredDeviceScreen(DpiAwareWindow screenFor = null) {
            return LogResult(screenFor, GetForcedDeviceScreen(screenFor) ?? GetLastUsedDeviceScreen(screenFor));
        }

        [CanBeNull]
        public static Screen GetForcedDeviceScreen(DpiAwareWindow screenFor = null) {
            var screenName = AppearanceManager.Instance.ForceScreenName;
            var forcedScreen = Screen.AllScreens.FirstOrDefault(x => x.DeviceName == screenName);
            if (forcedScreen != null) {
                if (OptionVerboseMode) {
                    Helpers.Logging.Warning($"Forced: {screenName}, screens: {string.Join("\n", Screen.AllScreens.Select(x => x.WorkingArea))}");
                }
                return LogResult(screenFor, forcedScreen);
            }

            return LogResult(screenFor, AppearanceManager.Instance.KeepWithinSingleScreen ? GetLastUsedDeviceScreen(screenFor) : null);
        }
    }
}