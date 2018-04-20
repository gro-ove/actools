using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Window with some extra features and multi-monitor DPI awareness.
    /// </summary>
    public abstract partial class DpiAwareWindow : Window {
        public static bool OptionVerboseMode;

        public static event EventHandler NewWindowCreated;

        protected DpiAwareWindow() {
            Logging.SetParent(this);

            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SystemEvents.DisplaySettingsChanged += OnSystemEventsDisplaySettingsChanged;

            Owner = GetDefaultOwner(true);
            Logging.Debug($"New window of type {GetType().Name} created! Owner: " + (Owner == null ? @"none" : $@"W{Owner?.GetHashCode():X8}"));

            // Remove that annoying backspace-to-go-back gesture
            var backGestures = NavigationCommands.BrowseBack.InputGestures;
            for (var i = backGestures.Count - 1; i >= 0; i--) {
                if (backGestures[i] is KeyGesture g && g.Key == Key.Back && g.Modifiers == ModifierKeys.None) {
                    backGestures.Remove(g);
                }
            }

            NewWindowCreated?.Invoke(this, EventArgs.Empty);
        }

        #region Keep track of the last active window to make sure new ones are shown where needed
        [CanBeNull]
        public static DpiAwareWindow LastActiveWindow { get; private set; }

        public DateTime LastActivated { get; private set; }

        protected sealed override void OnActivated(EventArgs e) {
            Logging.Here();
            base.OnActivated(e);
            LastActiveWindow = this;
            LastActivated = DateTime.Now;
            SaveDefaultScreen();
            OnActivatedOverride();
        }
        #endregion

        #region Override Title to add title case
        public new static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(DpiAwareWindow), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.None, OnTitleChanged, CoerceTitle));

        private static object CoerceTitle(DependencyObject d, object basevalue) {
            return basevalue?.ToString().ToTitle();
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((DpiAwareWindow)d).Logging.Debug("Title: " + e.NewValue);
            ((Window)d).Title = e.NewValue as string ?? "";
        }

        public new string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        #endregion

        #region Dimming
        private DpiAwareWindow _dimmedOwner;

        private void DimOwner() {
            UndimOwner();

            _dimmedOwner = Owner as DpiAwareWindow;
            if (_dimmedOwner?.IsDimmed == false) {
                _dimmedOwner.IsDimmed = true;
            } else {
                _dimmedOwner = null;
            }

            Logging.Debug("Dimmed owner: " + (_dimmedOwner == null ? @"none" : $@"W{_dimmedOwner?.GetHashCode():X8}"));
        }

        private void UndimOwner() {
            Logging.Debug("Dimmed owner: " + (_dimmedOwner == null ? @"none" : $@"W{_dimmedOwner?.GetHashCode():X8}"));
            if (_dimmedOwner != null) {
                _dimmedOwner.IsDimmed = false;
                _dimmedOwner = null;
            }
        }

        public static readonly DependencyProperty IsDimmedProperty = DependencyProperty.Register(nameof(IsDimmed), typeof(bool),
                typeof(DpiAwareWindow));

        public bool IsDimmed {
            get => GetValue(IsDimmedProperty) as bool? == true;
            set => SetValue(IsDimmedProperty, value);
        }
        #endregion

        #region Various show-related stuff, including improved ShowDialog() method
        private Window GetDefaultOwner(bool allowCurrent) {
            var app = Application.Current;
            return Logging.Log("Default owner", app == null || ReferenceEquals(app.MainWindow, this) ? null
                    : app.Windows.OfType<DpiAwareWindow>().Where(x => allowCurrent || !ReferenceEquals(x, Owner)).FirstOrDefault(x => x.IsActive)
                            ?? (app.MainWindow?.IsVisible == true ? app.MainWindow : null));
        }

        public static readonly DependencyProperty DoNotAttachToWaitingDialogsProperty = DependencyProperty.Register(nameof(DoNotAttachToWaitingDialogs),
                typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(false, (o, e) => { ((DpiAwareWindow)o)._doNotAttachToWaitingDialogs = (bool)e.NewValue; }));

        private bool _doNotAttachToWaitingDialogs;

        public bool DoNotAttachToWaitingDialogs {
            get => _doNotAttachToWaitingDialogs;
            set => SetValue(DoNotAttachToWaitingDialogsProperty, value);
        }

        public static readonly DependencyPropertyKey ShownAsDialogPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ShownAsDialog), typeof(bool),
                typeof(DpiAwareWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty ShownAsDialogProperty = ShownAsDialogPropertyKey.DependencyProperty;

        public bool ShownAsDialog => (bool)GetValue(ShownAsDialogProperty);

        public new bool? ShowDialog() {
            var doNotAttachToWaitingDialogs = DoNotAttachToWaitingDialogs;
            if (Owner != null && (!Owner.IsVisible || doNotAttachToWaitingDialogs && Owner is WaitingDialog)) {
                var defaultOwner = GetDefaultOwner(false);
                Owner = doNotAttachToWaitingDialogs && defaultOwner is WaitingDialog ? null : defaultOwner;
            }

            Logging.Debug("Show dialog! Owner: " + (Owner == null ? @"none" : $@"W{Owner?.GetHashCode():X8}"));
            DimOwner();

            if (Owner == null || Owner.Visibility == Visibility.Hidden) {
                Logging.Debug("Show in taskbar: owner is either missing or hidden");
                ShowInTaskbar = true;
            }

            try {
                SetValue(ShownAsDialogPropertyKey, true);
                return base.ShowDialog();
            } finally {
                SetValue(ShownAsDialogPropertyKey, false);
                UndimOwner();
            }
        }

        public Task<bool?> ShowDialogAsync() {
            var completion = new TaskCompletionSource<bool?>();
            Dispatcher.BeginInvoke(new Action(() => completion.SetResult(ShowDialog())));
            return completion.Task;
        }

        public Task ShowAndWaitAsync() {
            var task = new TaskCompletionSource<object>();
            Closed += (s, a) => task.SetResult(null);
            Show();
            Focus();
            return task.Task;
        }

        protected sealed override void OnClosing(CancelEventArgs e) {
            Logging.Debug("Closing… Owner: " + (Owner == null ? @"none" : $@"W{Owner?.GetHashCode():X8}"));
            base.OnClosing(e);
            if (e.Cancel) return;
            OnClosingOverride(e);
            try {
                if (!e.Cancel && IsActive) {
                    Owner?.Activate();
                }
            } catch (Exception ex) {
                Logging.Error(ex);
            }
        }

        protected sealed override void OnClosed(EventArgs e) {
            Logging.Here();
            _closed = true;
            UndimOwner();
            SystemEvents.DisplaySettingsChanged -= OnSystemEventsDisplaySettingsChanged;
            base.OnClosed(e);
            OnClosedOverride();
        }
        #endregion

        #region Safer implementation of the Close() method
        private bool _closed;

        public bool IsClosed() {
            return _closed;
        }

        public new void Close() {
            _closed = true;
            Logging.Here();
            try {
                UndimOwner();
                base.Close();
            } catch (InvalidOperationException e) {
                Logging.Error(e.Message);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }
        #endregion

        #region Fix for Alt+<> bindings
        // TODO: Test and debug if needed
        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && VisualExtension.IsInputFocused()) {
                var toRemove = InputBindings.OfType<InputBinding>().Where(x =>
                        x.Gesture is KeyGesture gesture && gesture.Modifiers.HasFlag(ModifierKeys.Alt)).ToList();
                foreach (var binding in toRemove) {
                    InputBindings.Remove(binding);
                }
                RestoreBindingsLater(toRemove).Forget();
            }

            base.OnPreviewKeyDown(e);
        }

        private async Task RestoreBindingsLater(IEnumerable<InputBinding> bindings) {
            await Task.Yield();
            foreach (var binding in bindings) {
                InputBindings.Add(binding);
            }
        }
        #endregion

        #region Some ready-to-override methods to decrease the amount of self-listeners
        protected virtual void OnActivatedOverride() { }
        protected virtual void OnLoadedOverride() { }
        protected virtual void OnUnloadedOverride() { }
        protected virtual void OnLocationChangedOverride() { }
        protected virtual void OnSizeChangedOverride(SizeChangedEventArgs e) { }
        protected virtual void OnStateChangedOverride() { }
        protected virtual void OnClosingOverride(CancelEventArgs e) { }
        protected virtual void OnClosedOverride() { }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            Logging.Here();
            FixFullscreen();
            SetBackgroundBlurIfNeeded();
            SetExtraFlagsIfNeeded();
            OnLoadedOverride();
        }

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            OnUnloadedOverride();
        }
        #endregion

        #region Methods to save state
        protected sealed override void OnLocationChanged(EventArgs e) {
            Logging.Here();
            base.OnLocationChanged(e);
            UpdateActualLocation();
            SaveLocationAndSize();
            OnLocationChangedOverride();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            Logging.Here();
            UpdateReferenceSizeForDpiAwareness();
            UpdateActualLocation();
            SaveLocationAndSize();
            OnSizeChangedOverride(e);
        }

        protected sealed override void OnStateChanged(EventArgs e) {
            Logging.Here();
            base.OnStateChanged(e);
            UpdateActualLocation();
            SaveLocationAndSize();
            OnStateChangedOverride();
        }
        #endregion

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

        public void ShowInvisible() {
            var needToShowInTaskbar = ShowInTaskbar;
            var initialWindowState = WindowState;

            try {
                ShowInTaskbar = false;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Opacity = 0d;

                Show();
                Hide();
                Opacity = 1d;
            } finally {
                ShowInTaskbar = needToShowInTaskbar;
                WindowState = initialWindowState;
            }
        }
    }
}