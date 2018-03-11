using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public partial class WebBlock {
        [CanBeNull]
        public static IWebDownloadListener DefaultDownloadListener { get; set; }

        public static event EventHandler<NewWindowEventArgs> NewTabGlobal;

        public WebBlock() {
            InitializeComponent();
        }

        [NotNull]
        public BetterObservableCollection<WebTab> Tabs { get; } = new BetterObservableCollection<WebTab>();

        private static readonly List<Stored> StayingAlive = new List<Stored>();

        private class Stored : IWithId {
            public Stored(string id, IEnumerable<WebTab> tabs, WebTab selected, bool alwaysKeepAlive) {
                Id = id;
                Tabs = tabs.ToArray();
                Selected = selected;
                AlwaysKeepAlive = alwaysKeepAlive;
            }

            public string Id { get; }
            public WebTab[] Tabs { get; }
            public WebTab Selected { get; }
            public bool AlwaysKeepAlive { get; }
        }

        public static void Unload(string keepAliveKey) {
            var alive = StayingAlive.GetByIdOrDefault(keepAliveKey);
            if (alive != null) {
                alive.Tabs.ForEach(y => y.OnUnloaded());
                StayingAlive.Remove(alive);
            }
        }

        public static void ResetStayingAlive() {
            StayingAlive.ForEach(x => x.Tabs.ForEach(y => y.OnUnloaded()));
            StayingAlive.Clear();
        }

        static WebBlock() {
            GameWrapper.Started += OnGameStarted;
            SettingsHolder.WebBlocks.PropertyChanged += OnWebBlocksSettingChanged;
            PluginsManager.Instance.PluginEnabled += OnPluginChanged;
            PluginsManager.Instance.PluginDisabled += OnPluginChanged;
        }

        private static void OnPluginChanged(object sender, PluginEventArgs args) {
            if (args.PluginId == KnownPlugins.CefSharp) {
                ResetStayingAlive();
            }
        }

        private static void OnWebBlocksSettingChanged(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(SettingsHolder.WebBlocks.KeepInMemory)
                    || args.PropertyName == nameof(SettingsHolder.WebBlocks.AlwaysKeepImportantInMemory)) {
                ResetStayingAlive();
            }
        }

        private static void OnGameStarted(object sender, GameStartedArgs gameStartedArgs) {
            if (SettingsHolder.WebBlocks.UnloadBeforeRace) {
                for (var i = StayingAlive.Count - 1; i >= 0; i--) {
                    var stored = StayingAlive[i];
                    if (!SettingsHolder.WebBlocks.AlwaysKeepImportantInMemory || !stored.AlwaysKeepAlive) {
                        stored.Tabs.ForEach(x => x.OnUnloaded());
                        StayingAlive.Remove(stored);
                    }
                }
            }
        }

        public static readonly DependencyProperty KeepAliveKeyProperty = DependencyProperty.Register(nameof(KeepAliveKey), typeof(string),
                typeof(WebBlock));

        [CanBeNull]
        public string KeepAliveKey {
            get => (string)GetValue(KeepAliveKeyProperty);
            set => SetValue(KeepAliveKeyProperty, value);
        }

        public static readonly DependencyProperty AlwaysKeepAliveProperty = DependencyProperty.Register(nameof(AlwaysKeepAlive), typeof(bool),
                typeof(WebBlock));

        public bool AlwaysKeepAlive {
            get => (bool)GetValue(AlwaysKeepAliveProperty);
            set => SetValue(AlwaysKeepAliveProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (Tabs.Count > 0) return;

            var alive = StayingAlive.GetByIdOrDefault(KeepAliveKey);
            if (alive != null) {
                StayingAlive.Remove(alive);
                Tabs.AddRange(alive.Tabs);
                MainTab = Tabs.First();
                SetCurrentTab(alive.Selected);

                foreach (var tab in Tabs) {
                    tab.PropertyChanged += OnTabPropertyChanged;
                    tab.PageLoaded += OnTabPageLoaded;
                    tab.NewWindow += OnTabNewWindow;
                }
            } else {
                MainTab = CreateTab(SaveKey != null && SettingsHolder.WebBlocks.SaveMainUrl ? ValuesStorage.Get(SaveKey, StartPage) : StartPage);
                MainTab.IsMainTab = true;

                if (SettingsHolder.WebBlocks.SaveMainUrl && SettingsHolder.WebBlocks.SaveExtraTabs && SaveKey != null) {
                    foreach (var tab in ValuesStorage.GetStringList(SaveKey + ":tabs")) {
                        CreateTab(tab, true);
                    }

                    var current = ValuesStorage.Get<string>(SaveKey + ":current");
                    SetCurrentTab(Tabs.FirstOrDefault(x => x.ActiveUrl == current) ?? MainTab);
                } else {
                    SetCurrentTab(MainTab);
                }
            }

            Tabs.CollectionChanged -= OnTabsCollectionChanged;
            Tabs.CollectionChanged += OnTabsCollectionChanged;

            TabsList.ItemsSource = Tabs;
            this.OnActualUnload(() => Tabs.ForEach(x => x.OnUnloaded()));
        }

        private readonly Busy _saveTabsBusy = new Busy();

        private void SaveTabs() {
            if (SettingsHolder.WebBlocks.SaveMainUrl && SettingsHolder.WebBlocks.SaveExtraTabs && SaveKey != null) {
                _saveTabsBusy.Yield(() => {
                    if (Tabs.Count > 1) {
                        ValuesStorage.Storage.SetStringList(SaveKey + ":tabs", Tabs.ApartFrom(MainTab).Select(x => x.ActiveUrl));
                        ValuesStorage.Set(SaveKey + ":current", CurrentTab?.ActiveUrl);
                    } else {
                        ValuesStorage.Remove(SaveKey + ":tabs");
                        ValuesStorage.Remove(SaveKey + ":current");
                    }
                });
            }
        }

        private void OnTabsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
            SaveTabs();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (KeepAliveKey != null && (SettingsHolder.WebBlocks.KeepInMemory > 0 || AlwaysKeepAlive)) {
                var toRemove = StayingAlive.GetByIdOrDefault(KeepAliveKey)
                        ?? (StayingAlive.Count >= SettingsHolder.WebBlocks.KeepInMemory
                                ? StayingAlive.FirstOrDefault(x => !SettingsHolder.WebBlocks.AlwaysKeepImportantInMemory || !x.AlwaysKeepAlive) : null);
                if (toRemove != null) {
                    toRemove.Tabs.ForEach(x => x.OnUnloaded());
                    StayingAlive.Remove(toRemove);
                }

                StayingAlive.Add(new Stored(KeepAliveKey, Tabs, CurrentTab, AlwaysKeepAlive));
                foreach (var tab in Tabs) {
                    tab.PropertyChanged -= OnTabPropertyChanged;
                    tab.PageLoaded -= OnTabPageLoaded;
                    tab.NewWindow -= OnTabNewWindow;
                }

                SetCurrentTab(null);
                MainTab = null;
                Tabs.Clear();
            }
        }

        private void SetCurrentTab([CanBeNull] WebTab tab) {
            if (ReferenceEquals(CurrentTab, tab)) return;
            tab?.EnsureLoaded();
            CurrentTab = tab;
            TabsList.SelectedItem = tab;
            PageParent.Child = tab?.GetElement(Window.GetWindow(this) as DpiAwareWindow);
            UrlTextBox.Text = tab?.ActiveUrl ?? "";
            ProgressBar.Visibility = tab?.IsLoading == true ? Visibility.Visible : Visibility.Collapsed;
            CommandManager.InvalidateRequerySuggested();
            SaveTabs();
            CurrentTabChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTabsListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            SetCurrentTab((WebTab)TabsList.SelectedItem);
        }

        [CanBeNull]
        public WebTab MainTab { get; private set; }

        [CanBeNull]
        public WebTab CurrentTab { get; private set; }

        [NotNull]
        private WebTab CreateTab(string url, bool delayed = false) {
            var tab = new WebTab(url, PreferTransparentBackground, delayed);

            if (_jsBridgeFactory != null) {
                try {
                    _setJsBridgeCallback?.Invoke(tab.GetJsBridge(_jsBridgeFactory));
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t set JS bridge", e);
                }
            }

            if (UserAgent != null) {
                tab.SetUserAgent(UserAgent);
            }

            if (StyleProvider != null) {
                tab.SetStyleProvider(StyleProvider);
            }

            var downloadListener = DownloadListener ?? DefaultDownloadListener;
            if (downloadListener != null) {
                tab.SetDownloadListener(downloadListener);
            }

            tab.SetNewWindowsBehavior(NewWindowsBehavior);
            Tabs.Add(tab);

            tab.PropertyChanged += OnTabPropertyChanged;
            tab.PageLoaded += OnTabPageLoaded;
            tab.PageLoadingStarted += OnTabPageLoadingStarted;
            tab.NewWindow += OnTabNewWindow;
            return tab;
        }

        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(CurrentTab.IsClosed)) {
                var tab = (WebTab)sender;
                if (ReferenceEquals(tab, CurrentTab)) {
                    SetCurrentTab(Tabs.Previous(CurrentTab));
                }

                if (Tabs.Remove(tab)) {
                    tab.OnUnloaded();
                }
            } else if (ReferenceEquals(sender, CurrentTab)) {
                var tab = (WebTab)sender;
                if (args.PropertyName == nameof(CurrentTab.IsLoading)) {
                    ProgressBar.Visibility = CurrentTab?.IsLoading == true ? Visibility.Visible : Visibility.Collapsed;
                } else if (args.PropertyName == nameof(CurrentTab.ActiveUrl)) {
                    UrlTextBox.Text = tab.ActiveUrl ?? "";
                }
            }
        }

        private void OnTabPageLoaded(object sender, UrlEventArgs e) {
            var tab = (WebTab)sender;

            if (ReferenceEquals(tab, MainTab) && SaveKey != null && e.Url.StartsWith(@"http", StringComparison.OrdinalIgnoreCase)) {
                ValuesStorage.Set(SaveKey, e.Url);
            }

            PageLoaded?.Invoke(this, new WebTabEventArgs(tab));
        }

        private void OnTabPageLoadingStarted(object sender, UrlEventArgs e) {
            var tab = (WebTab)sender;
            PageLoading?.Invoke(this, new WebTabEventArgs(tab));
        }

        public event EventHandler CurrentTabChanged;
        public event EventHandler<WebTabEventArgs> PageLoaded;
        public event EventHandler<WebTabEventArgs> PageLoading;
        public event EventHandler<NewWindowEventArgs> NewWindow;

        public void OpenNewTab(string url) {
            SetCurrentTab(CreateTab(url));
        }

        private void OnTabNewWindow(object sender, NewWindowEventArgs e) {
            if (NewWindowsBehavior == NewWindowsBehavior.MultiTab) {
                NewTabGlobal?.Invoke(this, e);
                if (e.Cancel) return;

                OpenNewTab(e.Url);
            } else {
                NewWindow?.Invoke(this, e);
            }
        }

        [CanBeNull]
        private Func<WebTab, JsBridgeBase> _jsBridgeFactory;

        [CanBeNull]
        private Action<JsBridgeBase> _setJsBridgeCallback;

        /// <summary>
        /// If browser restored its old tabs instead of creating new ones, JS Bridges can’t be replaced.
        /// But you can get the list of them and change their state to actual.
        /// </summary>
        public void SetJsBridge<T>(Action<T> setJsBridgeCallback = null) where T : JsBridgeBase, new() {
            _jsBridgeFactory = tab => new T { Tab = tab };
            var t = Tabs.Select(x => x.GetJsBridge(_jsBridgeFactory)).Cast<T>().ToList();
            if (setJsBridgeCallback == null) {
                _setJsBridgeCallback = null;
            } else {
                _setJsBridgeCallback = b => setJsBridgeCallback((T)b);
                t.NonNull().ForEach(setJsBridgeCallback);
            }
        }

        /// <summary>
        /// If browser restored its old tabs instead of creating new ones, JS Bridges can’t be replaced.
        /// But you can get the list of them and change their state to actual.
        /// </summary>
        public void SetJsBridge<T>([CanBeNull] Func<T> factory, Action<T> setJsBridgeCallback = null) where T : JsBridgeBase {
            try {
                if (factory == null) {
                    _jsBridgeFactory = null;
                } else {
                    _jsBridgeFactory = tab => {
                        var result = factory();
                        result.Tab = tab;
                        return result;
                    };
                }

                var t = Tabs.Select(x => x.GetJsBridge(_jsBridgeFactory)).Cast<T>().ToList();
                if (setJsBridgeCallback == null) {
                    _setJsBridgeCallback = null;
                } else {
                    _setJsBridgeCallback = b => setJsBridgeCallback((T)b);
                    t.NonNull().ForEach(setJsBridgeCallback);
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t set JS bridge", e);
            }
        }

        public static readonly DependencyProperty IsAddressBarVisibleProperty = DependencyProperty.Register(nameof(IsAddressBarVisible), typeof(bool),
                typeof(WebBlock), new PropertyMetadata(true, OnIsAddressBarVisibleChanged));

        public bool IsAddressBarVisible {
            get => GetValue(IsAddressBarVisibleProperty) as bool? == true;
            set => SetValue(IsAddressBarVisibleProperty, value);
        }

        private static void OnIsAddressBarVisibleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnIsAddressBarVisibleChanged((bool)e.NewValue);
        }

        private void OnIsAddressBarVisibleChanged(bool newValue) {
            AddressBar.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public static readonly DependencyProperty PreferTransparentBackgroundProperty = DependencyProperty.Register(nameof(PreferTransparentBackground),
                typeof(bool), typeof(WebBlock));

        public bool PreferTransparentBackground {
            get => (bool)GetValue(PreferTransparentBackgroundProperty);
            set => SetValue(PreferTransparentBackgroundProperty, value);
        }

        public static readonly DependencyProperty NewWindowsBehaviorProperty = DependencyProperty.Register(nameof(NewWindowsBehavior),
                typeof(NewWindowsBehavior),
                typeof(WebBlock), new PropertyMetadata(NewWindowsBehavior.Ignore));

        public NewWindowsBehavior NewWindowsBehavior {
            get => (NewWindowsBehavior)GetValue(NewWindowsBehaviorProperty);
            set => SetValue(NewWindowsBehaviorProperty, value);
        }

        public static readonly DependencyProperty UserAgentProperty = DependencyProperty.Register(nameof(UserAgent), typeof(string),
                typeof(WebBlock), new PropertyMetadata(OnUserAgentChanged));

        public string UserAgent {
            get => (string)GetValue(UserAgentProperty);
            set => SetValue(UserAgentProperty, value);
        }

        private static void OnUserAgentChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnUserAgentChanged((string)e.NewValue);
        }

        private void OnUserAgentChanged([CanBeNull] string newValue) {
            if (newValue == null) return;
            Tabs.ForEach(x => x.SetUserAgent(newValue));
        }

        public static readonly DependencyProperty StyleProviderProperty = DependencyProperty.Register(nameof(StyleProvider), typeof(ICustomStyleProvider),
                typeof(WebBlock), new PropertyMetadata(OnStyleProviderChanged));

        [CanBeNull]
        public ICustomStyleProvider StyleProvider {
            get => (ICustomStyleProvider)GetValue(StyleProviderProperty);
            set => SetValue(StyleProviderProperty, value);
        }

        private static void OnStyleProviderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnStyleProviderChanged((ICustomStyleProvider)e.NewValue);
        }

        private void OnStyleProviderChanged(ICustomStyleProvider newValue) {
            Tabs.ForEach(x => x.SetStyleProvider(newValue));
        }

        public static readonly DependencyProperty DownloadListenerProperty = DependencyProperty.Register(nameof(DownloadListener), typeof(IWebDownloadListener),
                typeof(WebBlock), new PropertyMetadata(OnDownloadListenerChanged));

        [CanBeNull]
        public IWebDownloadListener DownloadListener {
            get => (IWebDownloadListener)GetValue(DownloadListenerProperty);
            set => SetValue(DownloadListenerProperty, value);
        }

        private static void OnDownloadListenerChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnDownloadListenerChanged((IWebDownloadListener)e.NewValue);
        }

        private void OnDownloadListenerChanged(IWebDownloadListener newValue) {
            Tabs.ForEach(x => x.SetDownloadListener(newValue ?? DefaultDownloadListener));
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(WebBlock));

        [CanBeNull]
        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        public static readonly DependencyProperty StartPageProperty = DependencyProperty.Register(nameof(StartPage), typeof(string),
                typeof(WebBlock), new PropertyMetadata(OnStartPageChanged));

        [CanBeNull]
        public string StartPage {
            get => (string)GetValue(StartPageProperty);
            set => SetValue(StartPageProperty, value);
        }

        private static void OnStartPageChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnStartPageChanged((string)e.NewValue);
        }

        private void OnStartPageChanged([CanBeNull] string newValue) {
            if (IsLoaded) {
                MainTab?.Navigate(newValue);
            }
        }

        public static readonly DependencyProperty AddressBarExtraTemplateProperty = DependencyProperty.Register(nameof(AddressBarExtraTemplate), typeof(DataTemplate),
                typeof(WebBlock), new PropertyMetadata(OnAddressBarExtraTemplateChanged));

        public DataTemplate AddressBarExtraTemplate {
            get => (DataTemplate)GetValue(AddressBarExtraTemplateProperty);
            set => SetValue(AddressBarExtraTemplateProperty, value);
        }

        private static void OnAddressBarExtraTemplateChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((WebBlock)o).OnAddressBarExtraTemplateChanged((DataTemplate)e.NewValue);
        }

        private void OnAddressBarExtraTemplateChanged(DataTemplate newValue) {
            AddressBarExtra.ContentTemplate = newValue;
            AddressBarExtra.Visibility = Visibility.Visible;
        }

        private void OnUrlTextBoxKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                CurrentTab?.Navigate(UrlTextBox.Text);
            }
        }

        private void OnUrlTextBoxKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = CurrentTab?.BackCommand.CanExecute(null) == true;
            e.Handled = true;
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.BackCommand.Execute(null);
            e.Handled = true;
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = CurrentTab?.ForwardCommand.CanExecute(null) == true;
            e.Handled = true;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.ForwardCommand.Execute(null);
            e.Handled = true;
        }

        private void GoToPage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void GoToPage_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.Navigate(UrlTextBox.Text);
            e.Handled = true;
        }

        private void BrowseHome_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void BrowseHome_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.Navigate(StartPage);
            e.Handled = true;
        }

        private bool _usedForScrolling;

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if (Mouse.RightButton == MouseButtonState.Pressed && Tabs.Count > 1) {
                e.Handled = true;
                _usedForScrolling = true;
                SetCurrentTab(e.Delta < 0 ? Tabs.Next(CurrentTab) : Tabs.Previous(CurrentTab));
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            _usedForScrolling = false;
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (_usedForScrolling) {
                e.Handled = true;
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            e.Handled = true;
        }
    }
}