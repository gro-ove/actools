using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Win32;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract partial class DpiAwareWindow {
        public static readonly double UnlimitedSize = 99999d;

        private static readonly bool IsPerMonitorDpiAware = ModernUiHelper.TrySetPerMonitorDpiAware();
        private const double BaseDpi = 96d;

        [CanBeNull]
        private DpiInformation _dpi;

        [CanBeNull]
        private HwndSource _hwndSource;

        public double ScaleX => _dpi?.ScaleX ?? 1d;
        public double ScaleY => _dpi?.ScaleY ?? 1d;

        /// <summary>
        /// Put all window-size-related stuff here, not in OnLoaded!
        /// </summary>
        protected virtual void OnSourceInitializedOverride() { }

        protected sealed override void OnSourceInitialized(EventArgs e) {
            Logging.Here();

            base.OnSourceInitialized(e);

            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (_hwndSource?.CompositionTarget != null) {
                var matrix = _hwndSource.CompositionTarget.TransformToDevice;
                _dpi = new DpiInformation(BaseDpi * matrix.M11, BaseDpi * matrix.M22);
                DeviceScaleX = matrix.M11;
                DeviceScaleY = matrix.M22;
            } else {
                _dpi = new DpiInformation(BaseDpi, BaseDpi);
            }

            _dpi.UpdateUserScale(AppearanceManager.Instance.AppScale);
            Logging.Debug($"DPI: {_dpi}");

            LoadLocationAndSize();
            UpdateReferenceSizeForDpiAwareness();

            if (IsPerMonitorDpiAware) {
                Logging.Debug("Per-monitor DPI-aware");
                _hwndSource?.AddHook(WndProc);
            } else {
                Logging.Error("Not per-monitor DPI-aware!");
            }

            RefreshMonitorDpi();
            UpdateScaleRelatedParams();
            OnSourceInitializedOverride();
        }

        private void OnSystemEventsDisplaySettingsChanged(object sender, EventArgs e) {
            if (_hwndSource != null && WindowState == WindowState.Minimized) {
                Logging.Here();
                RefreshMonitorDpi();
            }
        }

        private void RefreshMonitorDpi() {
            var dpi = _dpi;
            Logging.Debug($"DPI: {dpi}, per-monitor DPI-aware: {IsPerMonitorDpiAware}, HWND src.: {_hwndSource}");
            if (!IsPerMonitorDpiAware || _hwndSource == null || dpi == null) return;
            var monitor = NativeMethods.MonitorFromWindow(_hwndSource.Handle, NativeMethods.MonitorDefaultToNearest);
            if (NativeMethods.GetDpiForMonitor(monitor, (int)MonitorDpiType.EffectiveDpi, out var dpiX, out var dpiY) == NativeMethods.SOk) {
                dpi.UpdateMonitorDpi(dpiX, dpiY);
                Logging.Debug($"DPI: {dpi}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            switch ((WindowMessage)msg) {
                case WindowMessage.SystemCommand:
                    var command = (WindowSystemCommand)(wParam.ToInt32() & 0xfff0);
                    if (command == WindowSystemCommand.Move && PreferredFullscreenMode) {
                        Logging.Debug("Cancel movement in preferred fullscreen mode");
                        handled = true;
                    }
                    break;
                case WindowMessage.EnterSizeMove:
                    Logging.Debug("Movement started");
                    OnDragged(true);
                    break;
                case WindowMessage.ExitSizeMove:
                    Logging.Debug("Movement ended");
                    OnDragged(false);
                    break;
                case WindowMessage.DpiChanged:
                    var dpiX = (double)(wParam.ToInt32() >> 16);
                    var dpiY = (double)(wParam.ToInt32() & 0x0000FFFF);
                    Logging.Debug($"DPI changed: {dpiX}, {dpiY}");
                    if (_dpi != null && _dpi.UpdateMonitorDpi(dpiX, dpiY)) {
                        UpdateScaleRelatedParams();
                    }
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        public void SetAppScale(double value) {
            Logging.Debug(value);
            if (_dpi != null && _dpi.UpdateUserScale(value)) {
                UpdateScaleRelatedParams();
            }
        }

        private bool _isBeingDragged;

        private void OnDragged(bool isDragged) {
            if (_isBeingDragged == isDragged) return;
            _isBeingDragged = isDragged;
            if (!isDragged) {
                UpdateScaleRelatedParams();
            }
        }

        private Size _originalMinSize, _originalMaxSize, _windowSize;
        private readonly Busy _updateSizeForDpiAwarenessBusy = new Busy();

        private void UpdateReferenceSizeForDpiAwareness() {
            var dpi = _dpi;

            Logging.Debug(dpi);
            if (dpi == null) return;

            _updateSizeForDpiAwarenessBusy.Yield(() => {
                _windowSize.Width = ActualWidth / dpi.ScaleX;
                _windowSize.Height = ActualHeight / dpi.ScaleY;
                Logging.Debug($"Reference size: {_windowSize.Width}×{_windowSize.Height}");
            });
        }

        private void SaveOriginalLimitations() {
            if (_originalMinSize != default(Size)) {
                Logging.Debug("Original limitations already saved");
                return;
            }

            _originalMinSize.Width = MinWidth;
            _originalMinSize.Height = MinHeight;
            _originalMaxSize.Width = IsFinite(MaxWidth) ? MaxWidth : UnlimitedSize;
            _originalMaxSize.Height = IsFinite(MaxHeight) ? MaxHeight : UnlimitedSize;
            Logging.Debug($"Original limitations: {_originalMinSize.Width}×{_originalMinSize.Height}");
        }

        private void UpdateLimitations(WpfScreen screen, double scaleX, double scaleY) {
            SaveOriginalLimitations();

            Logging.Debug($"{_originalMinSize.Width * scaleX}×{_originalMinSize.Height * scaleY} ({scaleX}, {scaleY}); {screen.WorkingArea}");
            MaxWidth = _originalMaxSize.Width * (_originalMaxSize.Width < UnlimitedSize ? scaleX : 1d);
            MaxHeight = _originalMaxSize.Height * (_originalMaxSize.Height < UnlimitedSize ? scaleY : 1d);
            MinWidth = Math.Min(screen.WorkingArea.Width, _originalMinSize.Width * scaleX);
            MinHeight = Math.Min(screen.WorkingArea.Height, _originalMinSize.Height * scaleY);

            Logging.Debug($"Result: {MinWidth}×{MinHeight}");

            if (ActualWidth > MaxWidth) {
                Width = MaxWidth;
            } else if (ActualWidth < MinWidth) {
                Width = MinWidth;
                Logging.Debug($"Clamp width to: {MinWidth}");
            }

            if (ActualHeight > MaxHeight) {
                Height = MaxHeight;
            } else if (ActualHeight < MinHeight) {
                Height = MinHeight;
                Logging.Debug($"Clamp width to: {MinHeight}");
            }
        }

        private double _currentScaleX = 1d, _currentScaleY = 1d;
        private bool _firstRun = true;

        private void UpdateScaleRelatedParams() {
            if (_isBeingDragged) {
                Logging.Debug("Window is being dragged at the moment, skipping");
                return;
            }

            var dpi = _dpi;
            Logging.Debug($"DPI: {dpi?.ToString() ?? @"none"}");

            if (dpi == null || dpi.ScaleX == _currentScaleX && dpi.ScaleY == _currentScaleY) {
                Logging.Debug($"Nothing to do: {dpi?.ScaleX}×{dpi?.ScaleY}, {_currentScaleX}×{_currentScaleY}");
                if (_firstRun) {
                    UpdateTextFormatting();
                }
                return;
            }

            _currentScaleX = dpi.ScaleX;
            _currentScaleY = dpi.ScaleY;
            _firstRun = false;

            var screen = GetScreen();
            Logging.Debug($"Screen: {screen}");
            UpdateLimitations(screen, dpi.ScaleX, dpi.ScaleY);

            var windowSize = _windowSize;
            if (windowSize != default(Size)) {
                Logging.Debug($"Update window size: {windowSize.Width}×{windowSize.Height}; scale: {dpi.ScaleX}, {dpi.ScaleY}");
                Width = windowSize.Width * dpi.ScaleX;
                Height = windowSize.Height * dpi.ScaleY;
                EnsureOnScreen(screen);
            } else {
                Logging.Debug("Reference window size is not known");
                EnsureOnScreen(screen);

                // Why saving it here?
                SaveLocationAndSize();
            }

            var root = (FrameworkElement)GetVisualChild(0);
            if (root != null) {
                Logging.Debug($"Set UI transform: {dpi.ScaleX}, {dpi.ScaleY}");
                root.LayoutTransform = dpi.IsScaled ? new ScaleTransform(dpi.ScaleX, dpi.ScaleY) : null;
            }

            UpdateTextFormatting();
        }

        public void UpdateTextFormatting() {
            var dpi = _dpi;
            if (dpi == null) return;
            if (AppearanceManager.Instance.IdealFormattingMode == null) {
                Resources[AppearanceManager.KeyFormattingMode] = dpi.IsScaled || DeviceScaleX != 1d || DeviceScaleY != 1d
                        ? TextFormattingMode.Ideal : TextFormattingMode.Display;
            } else {
                Resources.Remove(AppearanceManager.KeyFormattingMode);
            }
        }

        private static bool IsFinite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static readonly DependencyPropertyKey IconPathThicknessPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IconPathThickness),
                typeof(double), typeof(DpiAwareWindow), new PropertyMetadata(1d));

        public static readonly DependencyProperty IconPathThicknessProperty = IconPathThicknessPropertyKey.DependencyProperty;
        public double IconPathThickness => GetValue(IconPathThicknessProperty) as double? ?? 0d;
    }
}