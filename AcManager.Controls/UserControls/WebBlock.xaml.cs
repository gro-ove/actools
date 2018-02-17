using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls.Web;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls {
    public partial class WebBlock {
        public WebBlock() {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Loaded -= OnLoaded;
            MainTab = CreateTab((SaveKey == null ? null : ValuesStorage.Get<string>(SaveKey)) ?? StartPage);
            SetCurrentTab(MainTab);
            TabsList.ItemsSource = Tabs;
            this.OnActualUnload(() => {
                Tabs.ForEach(x => x.Something.OnUnloaded());
            });
        }

        private void SetCurrentTab([NotNull] WebTab tab) {
            if (ReferenceEquals(CurrentTab, tab)) return;
            CurrentTab = tab;
            TabsList.SelectedItem = tab;
            PageParent.Child = tab.Element;
            UrlTextBox.Text = tab.ActiveUrl;
            ProgressBar.Visibility = tab.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnTabsListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            SetCurrentTab((WebTab)TabsList.SelectedItem);
        }

        public BetterObservableCollection<WebTab> Tabs { get; } = new BetterObservableCollection<WebTab>();

        [CanBeNull]
        public WebTab MainTab { get; private set; }

        [CanBeNull]
        public WebTab CurrentTab { get; private set; }

        [NotNull]
        private WebTab CreateTab(string url) {
            var tab = new WebTab(url);
            var something = tab.Something;

            if (_scriptProvider != null) {
                something.SetScriptProvider(_scriptProvider.ForkFor(tab));
            }

            if (UserAgent != null) {
                something.SetUserAgent(UserAgent);
            }

            if (StyleProvider != null) {
                something.SetStyleProvider(StyleProvider);
            }

            something.SetNewWindowsBehavior(NewWindowsBehavior);
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

        private ScriptProviderBase _scriptProvider;

        public void SetScriptProvider(ScriptProviderBase provider) {
            _scriptProvider = provider;
            Tabs.ForEach(x => {
                x.Something.SetScriptProvider(provider.ForkFor(x));
            });
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

        public static readonly DependencyProperty NewWindowsBehaviorProperty = DependencyProperty.Register(nameof(NewWindowsBehavior), typeof(NewWindowsBehavior),
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
            Tabs.ForEach(x => x.Something.SetUserAgent(newValue));
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
            Tabs.ForEach(x => x.Something.SetStyleProvider(newValue));
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

        private void UrlTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                CurrentTab?.Navigate(UrlTextBox.Text);
            }
        }

        private void UrlTextBox_KeyUp(object sender, KeyEventArgs e) {
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
    }
}
