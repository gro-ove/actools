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
        protected virtual void OnSourceInitializedOverride() {}

        protected sealed override void OnSourceInitialized(EventArgs e) {
            /*if (RenderOptions.ProcessRenderMode != RenderMode.SoftwareOnly) {
                var ptr = new IntPtr(42);
                Marshal.StructureToPtr(42, ptr, true);
            }*/

            base.OnSourceInitialized(e);
            LoadLocationAndSize();

            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (_hwndSource?.CompositionTarget != null) {
                var matrix = _hwndSource.CompositionTarget.TransformToDevice;
                _dpi = new DpiInformation(BaseDpi * matrix.M11, BaseDpi * matrix.M22);
            } else {
                _dpi = new DpiInformation(BaseDpi, BaseDpi);
            }

            _dpi.UpdateUserScale(AppearanceManager.Current.AppScale);
            UpdateSizeForDpiAwareness();

            if (IsPerMonitorDpiAware) {
                _hwndSource?.AddHook(WndProc);
            }

            RefreshMonitorDpi();
            UpdateScaleRelatedParams();
            OnSourceInitializedOverride();
            SetBackgroundBlurIfNeeded();
            SetExtraFlagsIfNeeded();
        }

        private void OnSystemEventsDisplaySettingsChanged(object sender, EventArgs e) {
            if (_hwndSource != null && WindowState == WindowState.Minimized) {
                RefreshMonitorDpi();
            }
        }

        private void RefreshMonitorDpi() {
            if (!IsPerMonitorDpiAware || _hwndSource == null) return;
            var monitor = NativeMethods.MonitorFromWindow(_hwndSource.Handle, NativeMethods.MonitorDefaultToNearest);
            if (NativeMethods.GetDpiForMonitor(monitor, (int)MonitorDpiType.EffectiveDpi, out var dpiX, out var dpiY) == NativeMethods.SOk) {
                _dpi?.UpdateMonitorDpi(dpiX, dpiY);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            switch ((WindowMessage)msg) {
                case WindowMessage.SystemCommand:
                    var command = (WindowSystemCommand)(wParam.ToInt32() & 0xfff0);
                    if (command == WindowSystemCommand.Move && PreferredFullscreenMode) {
                        handled = true;
                    }
                    break;
                case WindowMessage.EnterSizeMove:
                    OnDragged(true);
                    break;
                case WindowMessage.ExitSizeMove:
                    OnDragged(false);
                    break;
                case WindowMessage.DpiChanged:
                    var dpiX = (double)(wParam.ToInt32() >> 16);
                    var dpiY = (double)(wParam.ToInt32() & 0x0000FFFF);
                    if (_dpi != null && _dpi.UpdateMonitorDpi(dpiX, dpiY)) {
                        UpdateScaleRelatedParams();
                    }
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        }

        public void SetAppScale(double value) {
            if (_dpi != null && _dpi.UpdateUserScale(value)) {
                UpdateScaleRelatedParams();
            }
        }

        private bool _isDragged;

        private void OnDragged(bool isDragged) {
            if (_isDragged == isDragged) return;
            _isDragged = isDragged;
            if (!isDragged) {
                UpdateScaleRelatedParams();
            }
        }

        private Size _originalMinSize, _originalMaxSize, _windowSize;
        private readonly Busy _updateSizeForDpiAwarenessBusy = new Busy();

        private void UpdateSizeForDpiAwareness() {
            var dpi = _dpi;
            if (dpi == null) return;

            _updateSizeForDpiAwarenessBusy.Yield(() => {
                _windowSize.Width = ActualWidth / dpi.ScaleX;
                _windowSize.Height = ActualHeight / dpi.ScaleY;
            });
        }

        private void SaveOriginalLimitations() {
            if (_originalMinSize != default(Size)) return;
            _originalMinSize.Width = MinWidth;
            _originalMinSize.Height = MinHeight;
            _originalMaxSize.Width = IsFinite(MaxWidth) ? MaxWidth : UnlimitedSize;
            _originalMaxSize.Height = IsFinite(MaxHeight) ? MaxHeight : UnlimitedSize;
        }

        private void UpdateLimitations(double scaleX, double scaleY) {
            SaveOriginalLimitations();
            MaxWidth = _originalMaxSize.Width * (_originalMaxSize.Width < UnlimitedSize ? scaleX : 1d);
            MaxHeight = _originalMaxSize.Height * (_originalMaxSize.Height < UnlimitedSize ? scaleY : 1d);
            MinWidth = _originalMinSize.Width * scaleX;
            MinHeight = _originalMinSize.Height * scaleY;
            if (ActualWidth > MaxWidth) Width = MaxWidth;
            if (ActualHeight > MaxHeight) Height = MaxHeight;
            if (ActualWidth < MinWidth) Width = MinWidth;
            if (ActualHeight < MinHeight) Height = MinHeight;
        }

        private double _currentScaleX = 1d, _currentScaleY = 1d;
        private bool _firstRun = true;

        private void UpdateScaleRelatedParams() {
            if (_isDragged) return;

            var dpi = _dpi;
            if (dpi == null || dpi.ScaleX == _currentScaleX && dpi.ScaleY == _currentScaleY) {
                if (_firstRun) {
                    UpdateTextFormatting();
                }
                return;
            }

            _currentScaleX = dpi.ScaleX;
            _currentScaleY = dpi.ScaleY;
            _firstRun = false;
            UpdateLimitations(dpi.ScaleX, dpi.ScaleY);

            var windowSize = _windowSize;
            if (windowSize != default(Size)) {
                Width = windowSize.Width * dpi.ScaleX;
                Height = windowSize.Height * dpi.ScaleY;
                EnsureOnScreen();
            } else {
                EnsureOnScreen();
                SaveLocationAndSize();
            }

            var root = (FrameworkElement)GetVisualChild(0);
            if (root != null) {
                root.LayoutTransform = dpi.IsScaled ? new ScaleTransform(dpi.ScaleX, dpi.ScaleY) : null;
            }

            UpdateTextFormatting();
        }

        public void UpdateTextFormatting() {
            var dpi = _dpi;
            if (dpi == null) return;
            if (AppearanceManager.Current.IdealFormattingMode == null) {
                Resources[AppearanceManager.KeyFormattingMode] = dpi.IsScaled ? TextFormattingMode.Ideal : TextFormattingMode.Display;
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