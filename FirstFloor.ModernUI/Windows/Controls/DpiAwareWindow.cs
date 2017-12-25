using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Win32;
using Microsoft.Win32;
using Application = System.Windows.Application;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// A window instance that is capable of per-monitor DPI awareness when supported.
    /// </summary>
    public abstract class DpiAwareWindow : Window {
        public static event EventHandler NewWindowCreated;
        public static event EventHandler NewWindowOpened;

        private const double BaseDpi = 96d;
        public static readonly AppScaleProperty AppScale = new AppScaleProperty();

        public static double OptionScale {
            get => AppScale.Scale;
            set => AppScale.Scale = value;
        }

        /// <summary>
        /// Occurs when the system or monitor DPI for this window has changed.
        /// </summary>
        public event EventHandler DpiChanged;

        private HwndSource _source;
        private readonly bool _isPerMonitorDpiAware;

        /// <summary>
        /// Initializes a new instance of the <see cref="DpiAwareWindow"/> class.
        /// </summary>
        protected DpiAwareWindow() {
            SourceInitialized += OnSourceInitialized;
            LocationChanged += OnLocationChanged;
            SizeChanged += OnSizeChanged;
            StateChanged += OnStateChanged;
            Loaded += OnLoaded;
            Closing += OnClosing;

            // WM_DPICHANGED is not send when window is minimized, do listen to global display setting changes
            SystemEvents.DisplaySettingsChanged += OnSystemEventsDisplaySettingsChanged;

            // try to set per-monitor dpi awareness, before the window is displayed
            _isPerMonitorDpiAware = ModernUiHelper.TrySetPerMonitorDpiAware();

            // set the default owner
            var app = Application.Current;
            if (app != null && !ReferenceEquals(app.MainWindow, this)) {
                Owner = app.Windows.OfType<DpiAwareWindow>().FirstOrDefault(x => x.IsActive)
                        ?? (app.MainWindow.IsVisible ? app.MainWindow : null);
            }

            foreach (var gesture in NavigationCommands.BrowseBack.InputGestures.OfType<KeyGesture>()
                                                      .Where(x => x.Key == Key.Back && x.Modifiers == ModifierKeys.None)
                                                      .ToList()) {
                NavigationCommands.BrowseBack.InputGestures.Remove(gesture);
            }

            NewWindowCreated?.Invoke(this, EventArgs.Empty);
        }

        public new static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(DpiAwareWindow), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.None, OnTitleChanged, CoerceTitle));

        private static object CoerceTitle(DependencyObject d, object basevalue) {
            return basevalue?.ToString().ToTitle();
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((Window)d).Title = e.NewValue as string;
        }

        public new string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs) {
            try {
                if (IsActive) {
                    Owner?.Activate();
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private DpiAwareWindow _dimmedOwner;

        private void DimOwner() {
            _dimmedOwner = Owner as DpiAwareWindow;
            if (_dimmedOwner?.IsDimmed == false) {
                _dimmedOwner.IsDimmed = true;
            } else {
                _dimmedOwner = null;
            }
        }

        private void UndimOwner() {
            if (_dimmedOwner != null) {
                _dimmedOwner.IsDimmed = false;
                _dimmedOwner = null;
            }
        }

        public new bool? ShowDialog() {
            DimOwner();

            if (Owner == null || Owner.Visibility == Visibility.Hidden) {
                ShowInTaskbar = true;
            }

            try {
                return base.ShowDialog();
            } finally {
                UndimOwner();
            }
        }

        public Task ShowAndWaitAsync() {
            var task = new TaskCompletionSource<object>();
            Closed += (s, a) => task.SetResult(null);
            Show();
            Focus();
            return task.Task;
        }

        public Task<bool?> ShowDialogAsync() {
            var completion = new TaskCompletionSource<bool?>();
            Dispatcher.BeginInvoke(new Action(() => completion.SetResult(ShowDialog())));
            return completion.Task;
        }

        public new void Close() {
            try {
                base.Close();
            } catch (InvalidOperationException e) {
                Logging.Warning("Close error: " + e.Message);
            } catch (Exception e) {
                Logging.Warning("Close error: " + e);
            }
        }

        public double OriginalMinWidth, OriginalMinHeight, OriginalMaxWidth, OriginalMaxHeight;

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            NewWindowOpened?.Invoke(this, EventArgs.Empty);
            OriginalMinWidth = MinWidth;
            OriginalMinHeight = MinHeight;
            OriginalMaxWidth = MaxWidth;
            OriginalMaxHeight = MaxHeight;
            SetDpiMultiplier();
            UpdateSizeLimits();
            LoadLocationAndSize();
        }

        public static readonly DependencyPropertyKey IconPathThicknessPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IconPathThickness), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(1d));

        public static readonly DependencyProperty IconPathThicknessProperty = IconPathThicknessPropertyKey.DependencyProperty;

        public double IconPathThickness => GetValue(IconPathThicknessProperty) as double? ?? 0d;

        public void SetDpiMultiplier() {
            var multiplier = GetDpiMultiplier() * OptionScale;
            if (AppearanceManager.Current.IdealFormattingMode == null) {
                Resources[AppearanceManager.KeyFormattingMode] = multiplier == 1d ? TextFormattingMode.Display : TextFormattingMode.Ideal;
            } else {
                Resources.Remove(AppearanceManager.KeyFormattingMode);
            }

            SetValue(IconPathThicknessPropertyKey, 1d / multiplier);
        }

        internal void UpdateSizeLimits() {
            if (!IsLoaded) return;

            try {
                MinWidth = OriginalMinWidth * OptionScale;
                MinHeight = OriginalMinHeight * OptionScale;
                MaxWidth = OriginalMaxWidth * OptionScale;
                MaxHeight = OriginalMaxHeight * OptionScale;
                SetDpiMultiplier();
            } catch (Exception e) {
                Logging.Warning(e);
                MinWidth = 100;
                MinHeight = 100;
                MaxWidth = 99999;
                MaxHeight = 99999;
            }
        }

        protected override void OnLocationChanged(EventArgs e) {
            base.OnLocationChanged(e);
            SaveLocationAndSize();
        }

        public static readonly DependencyPropertyKey ActualLeftPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualLeft), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualLeftProperty = ActualLeftPropertyKey.DependencyProperty;

        public double ActualLeft => GetValue(ActualLeftProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualTopPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualTop), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualTopProperty = ActualTopPropertyKey.DependencyProperty;

        public double ActualTop => GetValue(ActualTopProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualRightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualRight), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualRightProperty = ActualRightPropertyKey.DependencyProperty;

        public double ActualRight => GetValue(ActualRightProperty) as double? ?? 0d;

        public static readonly DependencyPropertyKey ActualBottomPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActualBottom), typeof(double),
                typeof(DpiAwareWindow), new PropertyMetadata(0d));

        public static readonly DependencyProperty ActualBottomProperty = ActualBottomPropertyKey.DependencyProperty;

        public double ActualBottom => GetValue(ActualBottomProperty) as double? ?? 0d;

        private void UpdateActualLocation() {
            if (WindowState == WindowState.Maximized) {
                var rect = GetWindowRectangle();
                SetValue(ActualTopPropertyKey, (double)rect.Top);
                SetValue(ActualLeftPropertyKey, (double)rect.Left);
            } else {
                SetValue(ActualTopPropertyKey, Top);
                SetValue(ActualLeftPropertyKey, Left);
            }

            SetValue(ActualBottomPropertyKey, ActualTop + ActualHeight);
            SetValue(ActualRightPropertyKey, ActualLeft + ActualWidth);
        }

        protected void OnLocationChanged(object sender, EventArgs e) {
            UpdateActualLocation();
            SaveLocationAndSize();
        }

        protected virtual void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateActualLocation();
            SaveLocationAndSize();
        }

        [DllImport(@"user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out Win32Rect lpWindowRect);

        private Win32Rect GetWindowRectangle() {
            GetWindowRect(new WindowInteropHelper(this).Handle, out var rect);
            return rect;
        }

        protected void OnStateChanged(object sender, EventArgs e) {
            UpdateActualLocation();
            SaveLocationAndSize();
        }

        /// <summary>
        /// Gets the DPI information for this window instance.
        /// </summary>
        /// <remarks>
        /// DPI information is available after a window handle has been created.
        /// </remarks>
        public DpiInformation DpiInformation { get; private set; }

        /// <summary>
        /// Raises the System.Windows.Window.Closed event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e) {
            UndimOwner();
            base.OnClosed(e);

            // detach global event handlers
            SystemEvents.DisplaySettingsChanged -= OnSystemEventsDisplaySettingsChanged;
        }

        private void OnSystemEventsDisplaySettingsChanged(object sender, EventArgs e) {
            if (_source != null && WindowState == WindowState.Minimized) {
                RefreshMonitorDpi();
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e) {
            _source = (HwndSource)PresentationSource.FromVisual(this);
            if (_source?.CompositionTarget == null) return;

            // calculate the DPI used by WPF; this is the same as the system DPI
            var matrix = _source.CompositionTarget.TransformToDevice;
            DpiInformation = new DpiInformation(BaseDpi * matrix.M11, BaseDpi * matrix.M22);

            if (_isPerMonitorDpiAware) {
                _source.AddHook(WndProc);
            }

            RefreshMonitorDpi();
        }

        private readonly Busy _busy = new Busy();

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (msg != NativeMethods.WM_DPICHANGED || _source?.CompositionTarget == null) {
                return IntPtr.Zero;
            }

            // Marshal the value in the lParam into a Rect.
            var newDisplayRect = (Win32Rect)Marshal.PtrToStructure(lParam, typeof(Win32Rect));

            // Set the Window’s position & size.
            var matrix = _source.CompositionTarget.TransformFromDevice;
            var ul = matrix.Transform(new Vector(newDisplayRect.Left, newDisplayRect.Top));
            var hw = matrix.Transform(new Vector(newDisplayRect.Right - newDisplayRect.Left, newDisplayRect.Bottom - newDisplayRect.Top));

            // Get the new DPI settings from wParam
            var dpiX = (double)(wParam.ToInt32() >> 16);
            var dpiY = (double)(wParam.ToInt32() & 0x0000FFFF);

            _busy.DoDelayAfterwards(() => {
                Left = ul.X;
                Top = ul.Y;
                UpdateWindowSize(hw.X, hw.Y, dpiX);
            }, 100);

            // Remember the current DPI settings.
            var oldDpiX = DpiInformation.MonitorDpiX;
            var oldDpiY = DpiInformation.MonitorDpiY;

            if (oldDpiX != dpiX || oldDpiY != dpiY) {
                DpiInformation.UpdateMonitorDpi(dpiX, dpiY);

                // update layout scale
                UpdateLayoutTransform();

                // raise DpiChanged event
                OnDpiChanged(EventArgs.Empty);
            }

            handled = true;
            return IntPtr.Zero;
        }

        private void UpdateLayoutTransform() {
            if (!_isPerMonitorDpiAware) return;

            var root = (FrameworkElement)GetVisualChild(0);
            if (root == null) return;

            root.LayoutTransform = DpiInformation.ScaleX != 1.0 || DpiInformation.ScaleY != 1.0 ?
                    new ScaleTransform(DpiInformation.ScaleX, DpiInformation.ScaleY) : null;
        }

        private static bool IsFinite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double _dpi = BaseDpi;

        public bool IsDpiUnusual() {
            return _dpi != BaseDpi;
        }

        public double GetDpiMultiplier() {
            return _dpi / BaseDpi;
        }

        private void UpdateWindowSize(double width, double height, double dpi) {
            _dpi = dpi;
            SetDpiMultiplier();

            // determine relative scalex and scaley
            var relScaleX = width / Width;
            var relScaleY = height / Height;
            if (relScaleX == 1.0 && relScaleY == 1.0) return;

            if (!IsFinite(relScaleX)) {
                Logging.Warning("relScaleX is NaN: " + relScaleX + ", " + width + ", " + Width);
                return;
            }

            if (!IsFinite(relScaleY)) {
                Logging.Warning("relScaleY is NaN: " + relScaleY + ", " + width + ", " + Width);
                return;
            }

            // adjust window size constraints as well
            if (IsFinite(MaxWidth)) MaxWidth *= relScaleX;
            if (IsFinite(MaxHeight)) MaxHeight *= relScaleY;
            MinWidth *= relScaleX;
            MinHeight *= relScaleY;

            Width = width;
            Height = height;
        }

        private bool _scaled;

        private void RescaleIfNeeded() {
            if (OptionScale != 1d && !_scaled) {
                if (IsFinite(MaxWidth)) MaxWidth *= OptionScale;
                if (IsFinite(MaxHeight)) MaxHeight *= OptionScale;
                if (IsFinite(Width)) Width *= OptionScale;
                if (IsFinite(Height)) Height *= OptionScale;
                if (IsFinite(MinWidth)) MinWidth *= OptionScale;
                if (IsFinite(MinHeight)) MinHeight *= OptionScale;
                _scaled = true;
            }
        }

        /// <summary>
        /// Refreshes the current monitor DPI settings and update the window size and layout scale accordingly.
        /// </summary>
        protected void RefreshMonitorDpi() {
            RescaleIfNeeded();
            if (!_isPerMonitorDpiAware) {
                return;
            }

            // get the current DPI of the monitor of the window
            var monitor = NativeMethods.MonitorFromWindow(_source.Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);

            uint xDpi = 96;
            uint yDpi = 96;
            if (NativeMethods.GetDpiForMonitor(monitor, (int)MonitorDpiType.EffectiveDpi, ref xDpi, ref yDpi) != NativeMethods.S_OK) {
                xDpi = 96;
                yDpi = 96;
            }

            // vector contains the change of the old to new DPI
            var dpiVector = DpiInformation.UpdateMonitorDpi(xDpi, yDpi);

            // update Width and Height based on the current DPI of the monitor
            UpdateWindowSize(Width * dpiVector.X, Height * dpiVector.Y, xDpi);

            // update graphics and text based on the current DPI of the monitor
            UpdateLayoutTransform();
        }

        /// <summary>
        /// Raises the <see cref="E:DpiChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void OnDpiChanged(EventArgs e) {
            DpiChanged?.Invoke(this, e);
        }

        public static readonly DependencyProperty IsDimmedProperty = DependencyProperty.Register(nameof(IsDimmed), typeof(bool),
                typeof(DpiAwareWindow));

        public bool IsDimmed {
            get => GetValue(IsDimmedProperty) as bool? == true;
            set => SetValue(IsDimmedProperty, value);
        }

        public static void OnFatalError(Exception e) {
            var app = Application.Current;
            if (app != null){
                foreach (var result in app.Windows.OfType<DpiAwareWindow>()) {
                    result.IsDimmed = true;
                }
            }

            new FatalErrorMessage {
                Message = e.Message,
                StackTrace = e.ToString()
            }.ShowDialog();
        }

        public void ShowDialogWithoutBlocking() {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(new Action(() => ShowDialog()));
        }

        private const int GwlStyle = -16;
        private const int WsDisabled = 0x08000000;

        [DllImport(@"user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport(@"user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private bool _nativeEnabled;

        public bool NativeEnabled {
            get => _nativeEnabled;
            set {
                if (Equals(value, _nativeEnabled)) return;
                _nativeEnabled = value;

                var handle = new WindowInteropHelper(this).Handle;
                SetWindowLong(handle, GwlStyle, GetWindowLong(handle, GwlStyle) &
                        ~WsDisabled | (value ? 0 : WsDisabled));
            }
        }

        public static readonly DependencyProperty LocationAndSizeKeyProperty = DependencyProperty.Register(nameof(LocationAndSizeKey), typeof(string),
                typeof(DpiAwareWindow), new PropertyMetadata(OnLocationAndSizeKeyChanged));

        public string LocationAndSizeKey {
            get => (string)GetValue(LocationAndSizeKeyProperty);
            set => SetValue(LocationAndSizeKeyProperty, value);
        }

        private static void OnLocationAndSizeKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DpiAwareWindow)o).OnLocationAndSizeKeyChanged();
        }

        private bool _skipLoading;

        public void SetLocationAndSizeKeyAndSave(string key) {
            _skipLoading = true;
            try {
                LocationAndSizeKey = key;
                SaveLocationAndSize();
            } finally {
                _skipLoading = false;
            }
        }

        private void OnLocationAndSizeKeyChanged() {
            if (_skipLoading) return;
            LoadLocationAndSize();
        }

        private void LoadLocationAndSize() {
            var key = LocationAndSizeKey;
            if (key == null) return;

            try {
                RescaleIfNeeded();
                this.SetPlacement(ValuesStorage.GetString(key));
                Loaded += (sender, args) => this.IsWindowOnAnyScreen();
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private void SaveLocationAndSize() {
            var key = LocationAndSizeKey;
            if (key == null || WindowState == WindowState.Minimized || !IsLoaded) return;

            try {
                RescaleIfNeeded();
                ValuesStorage.Set(key, this.GetPlacement());
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void BringToFront() {
            if (!IsVisible) {
                Show();
            }

            if (WindowState == WindowState.Minimized) {
                WindowState = WindowState.Normal;
            }

            Topmost = true;
            Topmost = false;
            Focus();
        }
    }
}
