using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Window with some extra features and multi-monitor DPI awareness.
    /// </summary>
    public abstract partial class DpiAwareWindow : Window {
        public static event EventHandler NewWindowCreated;

        protected DpiAwareWindow() {
            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SystemEvents.DisplaySettingsChanged += OnSystemEventsDisplaySettingsChanged;

            Owner = GetDefaultOwner();

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

        protected sealed override void OnActivated(EventArgs e) {
            base.OnActivated(e);
            LastActiveWindow = this;
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
            ((Window)d).Title = e.NewValue as string;
        }

        public new string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        #endregion

        #region Dimming
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

        public static readonly DependencyProperty IsDimmedProperty = DependencyProperty.Register(nameof(IsDimmed), typeof(bool),
                typeof(DpiAwareWindow));

        public bool IsDimmed {
            get => GetValue(IsDimmedProperty) as bool? == true;
            set => SetValue(IsDimmedProperty, value);
        }
        #endregion

        #region Various show-related stuff, including improved ShowDialog() method
        private Window GetDefaultOwner() {
            var app = Application.Current;
            return app == null || ReferenceEquals(app.MainWindow, this) ? null
                    : app.Windows.OfType<DpiAwareWindow>().Where(x => !ReferenceEquals(x, Owner)).FirstOrDefault(x => x.IsActive)
                            ?? (app.MainWindow?.IsVisible == true ? app.MainWindow : null);
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
            if (Owner != null && (!Owner.IsVisible || DoNotAttachToWaitingDialogs && Owner is WaitingDialog)) {
                Owner = GetDefaultOwner();
            }

            DimOwner();

            if (Owner == null || Owner.Visibility == Visibility.Hidden) {
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
            base.OnClosing(e);
            if (e.Cancel) return;
            OnClosingOverride(e);
            try {
                if (!e.Cancel && IsActive) {
                    Owner?.Activate();
                }
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        protected sealed override void OnClosed(EventArgs e) {
            UndimOwner();
            SystemEvents.DisplaySettingsChanged -= OnSystemEventsDisplaySettingsChanged;
            base.OnClosed(e);
            OnClosedOverride();
        }
        #endregion

        #region Safer implementation of the Close() method
        public new void Close() {
            try {
                base.Close();
            } catch (InvalidOperationException e) {
                Logging.Warning("Close error: " + e.Message);
            } catch (Exception e) {
                Logging.Warning("Close error: " + e);
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
            FixFullscreen();
            OnLoadedOverride();
        }

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs) {
            OnUnloadedOverride();
        }
        #endregion

        #region Methods to save state
        protected sealed override void OnLocationChanged(EventArgs e) {
            base.OnLocationChanged(e);
            SaveLocationAndSize();
            OnLocationChangedOverride();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateSizeForDpiAwareness();
            SaveLocationAndSize();
            OnSizeChangedOverride(e);
        }

        protected sealed override void OnStateChanged(EventArgs e) {
            base.OnStateChanged(e);
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
    }
}