using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public partial class WebBlock {
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

        static WebBlock() {
            GameWrapper.Started += OnGameStarted;
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
            } else {
                MainTab = CreateTab((SaveKey == null ? null : ValuesStorage.Get<string>(SaveKey)) ?? StartPage);
                MainTab.IsMainTab = true;
                SetCurrentTab(MainTab);
            }

            TabsList.ItemsSource = Tabs;
            this.OnActualUnload(() => { Tabs.ForEach(x => x.OnUnloaded()); });
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
                SetCurrentTab(null);
                MainTab = null;
                Tabs.Clear();
            }
        }

        private void SetCurrentTab([CanBeNull] WebTab tab) {
            if (ReferenceEquals(CurrentTab, tab)) return;
            CurrentTab = tab;
            TabsList.SelectedItem = tab;
            PageParent.Child = tab?.GetElement(Window.GetWindow(this) as DpiAwareWindow);
            UrlTextBox.Text = tab?.ActiveUrl ?? "";
            ProgressBar.Visibility = tab?.IsLoading == true ? Visibility.Visible : Visibility.Collapsed;
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnTabsListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            SetCurrentTab((WebTab)TabsList.SelectedItem);
        }

        [CanBeNull]
        public WebTab MainTab { get; private set; }

        [CanBeNull]
        public WebTab CurrentTab { get; private set; }

        [NotNull]
        private WebTab CreateTab(string url) {
            var tab = new WebTab(url, PreferTransparentBackground);

            if (_jsBridgeFactory != null) {
                tab.SetJsBridge(_jsBridgeFactory);
            }

            if (UserAgent != null) {
                tab.SetUserAgent(UserAgent);
            }

            if (StyleProvider != null) {
                tab.SetStyleProvider(StyleProvider);
            }

            tab.SetNewWindowsBehavior(NewWindowsBehavior);
            Tabs.Add(tab);

            tab.PropertyChanged += OnTabPropertyChanged;
            tab.PageLoaded += OnTabPageLoaded;
            tab.NewWindow += OnTabNewWindow;
            return tab;
        }

        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs args) {
            if (ReferenceEquals(sender, CurrentTab)) {
                var tab = (WebTab)sender;
                if (args.PropertyName == nameof(CurrentTab.IsLoading)) {
                    ProgressBar.Visibility = CurrentTab?.IsLoading == true ? Visibility.Visible : Visibility.Collapsed;
                } else if (args.PropertyName == nameof(CurrentTab.ActiveUrl)) {
                    UrlTextBox.Text = tab.ActiveUrl;
                } else if (args.PropertyName == nameof(CurrentTab.IsClosed)) {
                    if (ReferenceEquals(tab, CurrentTab)) {
                        SetCurrentTab(Tabs.Previous(CurrentTab));
                    }

                    Tabs.Remove(tab);
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

        public event EventHandler<WebTabEventArgs> PageLoaded;

        private void OnTabNewWindow(object sender, UrlEventArgs e) {
            SetCurrentTab(CreateTab(e.Url));
        }

        private Func<WebTab, JsBridgeBase> _jsBridgeFactory;

        public void SetScriptProvider<T>() where T : JsBridgeBase, new() {
            _jsBridgeFactory = tab => new T { Tab = tab };
            Tabs.ForEach(x => x.SetJsBridge(_jsBridgeFactory));
        }

        public void SetScriptProvider(Func<JsBridgeBase> factory) {
            _jsBridgeFactory = tab => {
                var result = factory();
                result.Tab = tab;
                return result;
            };
            Tabs.ForEach(x => x.SetJsBridge(_jsBridgeFactory));
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
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.BackCommand.Execute(null);
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = CurrentTab?.ForwardCommand.CanExecute(null) == true;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.ForwardCommand.Execute(null);
        }

        private void GoToPage_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void GoToPage_Executed(object sender, ExecutedRoutedEventArgs a) {
            CurrentTab?.Navigate(UrlTextBox.Text);
        }

        private void BrowseHome_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void BrowseHome_Executed(object sender, ExecutedRoutedEventArgs e) {
            CurrentTab?.Navigate(StartPage);
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
    }
}