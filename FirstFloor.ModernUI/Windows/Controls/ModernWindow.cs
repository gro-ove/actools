using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Navigation;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Represents a Modern UI styled window.
    /// </summary>
    public class ModernWindow : DpiAwareWindow {
        /// <summary>
        /// Identifies the BackgroundContent dependency property.
        /// </summary>
        public static readonly DependencyProperty BackgroundContentProperty = DependencyProperty.Register("BackgroundContent", typeof(object), typeof(ModernWindow));
        /// <summary>
        /// Identifies the MenuLinkGroups dependency property.
        /// </summary>
        public static readonly DependencyProperty MenuLinkGroupsProperty = DependencyProperty.Register("MenuLinkGroups", typeof(LinkGroupCollection), typeof(ModernWindow));
        /// <summary>
        /// Identifies the TitleLinks dependency property.
        /// </summary>
        public static readonly DependencyProperty TitleLinksProperty = DependencyProperty.Register("TitleLinks", typeof(LinkCollection), typeof(ModernWindow));
        /// <summary>
        /// Identifies the IsTitleVisible dependency property.
        /// </summary>
        public static readonly DependencyProperty IsTitleVisibleProperty = DependencyProperty.Register("IsTitleVisible", typeof(bool), typeof(ModernWindow), new PropertyMetadata(false));

        /// <summary>
        /// Identifies the ContentLoader dependency property.
        /// </summary>
        public static readonly DependencyProperty ContentLoaderProperty = DependencyProperty.Register("ContentLoader", typeof(IContentLoader), typeof(ModernWindow), new PropertyMetadata(new DefaultContentLoader()));
        /// <summary>
        /// Identifies the LinkNavigator dependency property.
        /// </summary>
        public static DependencyProperty LinkNavigatorProperty = DependencyProperty.Register("LinkNavigator", typeof(ILinkNavigator), typeof(ModernWindow), new PropertyMetadata(new DefaultLinkNavigator()));

        private Storyboard _backgroundAnimation;

        public static RoutedUICommand NavigateTitleLink { get; } = new RoutedUICommand(ModernUI.Resources.NavigateLink, "NavigateTitleLink", typeof(LinkCommands));

        /// <summary>
        /// Initializes a new instance of the <see cref="ModernWindow"/> class.
        /// </summary>
        public ModernWindow() {
            DefaultStyleKey = typeof(ModernWindow);

            // create empty collections
            SetCurrentValue(MenuLinkGroupsProperty, new LinkGroupCollection());
            SetCurrentValue(TitleLinksProperty, new LinkCollection());

            // associate window commands with this instance
            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindow, OnCanResizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimizeWindow, OnCanMinimizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindow, OnCanResizeWindow));

            // associate navigate link command with this instance
            CommandBindings.Add(new CommandBinding(LinkCommands.NavigateLink, OnNavigateLink, OnCanNavigateLink));
            CommandBindings.Add(new CommandBinding(NavigateTitleLink, OnNavigateTitleLink, OnCanNavigateTitleLink));

            // listen for theme changes
            AppearanceManager.Current.PropertyChanged += OnAppearanceManagerPropertyChanged;

            // ContentSource = DefaultContentSource;
        }

        /// <summary>
        /// Raises the System.Windows.Window.Closed event.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            // detach event handler
            AppearanceManager.Current.PropertyChanged -= OnAppearanceManagerPropertyChanged;
        }

        public event EventHandler<NavigationEventArgs> FrameNavigated;
        private ModernMenu _menu;
        private ModernFrame _frame;

        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code or internal processes call System.Windows.FrameworkElement.ApplyTemplate().
        /// </summary>
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            
            if (_frame != null) {
                _frame.Navigated -= Frame_Navigated;
            }

            _frame = GetTemplateChild("ContentFrame") as ModernFrame;

            if (_frame != null) {
                _frame.Navigated += Frame_Navigated;
            }

            _menu = GetTemplateChild("PART_Menu") as ModernMenu;

            // retrieve BackgroundAnimation storyboard
            var border = GetTemplateChild("WindowBorder") as Border;
            if (border == null) return;

            _backgroundAnimation = border.Resources["BackgroundAnimation"] as Storyboard;
            _backgroundAnimation?.Begin();

            // ContentFrame
        }

        private void Frame_Navigated(object sender, NavigationEventArgs navigationEventArgs) {
            FrameNavigated?.Invoke(this, navigationEventArgs);
        }

        private void OnAppearanceManagerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            // start background animation if theme has changed
            if (e.PropertyName == "ThemeSource") {
                _backgroundAnimation?.Begin();
            }
        }

        private void OnCanNavigateTitleLink(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        public void NavigateTo(Uri uri) {
            _menu.SelectedSource = uri;
        }

        private void OnNavigateTitleLink(object sender, ExecutedRoutedEventArgs e) {
            if (LinkNavigator == null) return;

            var titleLink = e.Parameter as TitleLink;
            if (titleLink?.GroupKey != null) {
                _menu?.SwitchToGroupByKey(titleLink.GroupKey);
                return;
            }

            Uri uri;
            string parameter;
            string targetName;

            var eParameter = (e.Parameter as Link)?.Source ?? e.Parameter;
            if (NavigationHelper.TryParseUriWithParameters(eParameter, out uri, out parameter, out targetName)) {
                LinkNavigator.Navigate(uri, e.Source as FrameworkElement, parameter);
            }
        }

        private void OnCanNavigateLink(object sender, CanExecuteRoutedEventArgs e) {
            // true by default
            e.CanExecute = true;
            if (LinkNavigator?.Commands == null) return;

            // in case of command uri, check if ICommand.CanExecute is true
            Uri uri;
            string parameter;
            string targetName;

            // TODO: CanNavigate is invoked a lot, which means a lot of parsing. need improvements??
            if (!NavigationHelper.TryParseUriWithParameters(e.Parameter, out uri, out parameter, out targetName)) {
                return;
            }

            ICommand command;
            if (LinkNavigator.Commands.TryGetValue(uri, out command)) {
                e.CanExecute = command.CanExecute(parameter);
            }
        }

        private void OnNavigateLink(object sender, ExecutedRoutedEventArgs e) {
            if (LinkNavigator == null) return;

            Uri uri;
            string parameter;
            string targetName;

            if (NavigationHelper.TryParseUriWithParameters(e.Parameter, out uri, out parameter, out targetName)) {
                LinkNavigator.Navigate(uri, e.Source as FrameworkElement, parameter);
            }
        }

        private void OnCanResizeWindow(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ResizeMode == ResizeMode.CanResize || ResizeMode == ResizeMode.CanResizeWithGrip;
        }

        private void OnCanMinimizeWindow(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = ResizeMode != ResizeMode.NoResize;
        }

        private void OnCloseWindow(object target, ExecutedRoutedEventArgs e) {
            SystemCommands.CloseWindow(this);
        }

        private void OnMaximizeWindow(object target, ExecutedRoutedEventArgs e) {
            SystemCommands.MaximizeWindow(this);
        }

        private void OnMinimizeWindow(object target, ExecutedRoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void OnRestoreWindow(object target, ExecutedRoutedEventArgs e) {
            SystemCommands.RestoreWindow(this);
        }

        /// <summary>
        /// Gets or sets the background content of this window instance.
        /// </summary>
        public object BackgroundContent {
            get { return GetValue(BackgroundContentProperty); }
            set { SetValue(BackgroundContentProperty, value); }
        }

        /// <summary>
        /// Gets or sets the collection of link groups shown in the window’s menu.
        /// </summary>
        public LinkGroupCollection MenuLinkGroups {
            get { return (LinkGroupCollection)GetValue(MenuLinkGroupsProperty); }
            set { SetValue(MenuLinkGroupsProperty, value); }
        }

        /// <summary>
        /// Gets or sets the collection of links that appear in the menu in the title area of the window.
        /// </summary>
        public LinkCollection TitleLinks {
            get { return (LinkCollection)GetValue(TitleLinksProperty); }
            set { SetValue(TitleLinksProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the window title is visible in the UI.
        /// </summary>
        public bool IsTitleVisible {
            get { return (bool)GetValue(IsTitleVisibleProperty); }
            set { SetValue(IsTitleVisibleProperty, value); }
        }

        public static readonly DependencyProperty DefaultContentSourceProperty = DependencyProperty.Register(nameof(DefaultContentSource), typeof(Uri),
                typeof(ModernWindow));

        public Uri DefaultContentSource {
            get { return (Uri)GetValue(DefaultContentSourceProperty); }
            set { SetValue(DefaultContentSourceProperty, value); }
        }

        /// <summary>
        /// Gets or sets the content loader.
        /// </summary>
        public IContentLoader ContentLoader {
            get { return (IContentLoader)GetValue(ContentLoaderProperty); }
            set { SetValue(ContentLoaderProperty, value); }
        }

        /// <summary>
        /// Gets or sets the link navigator.
        /// </summary>
        /// <value>The link navigator.</value>
        public ILinkNavigator LinkNavigator {
            get { return (ILinkNavigator)GetValue(LinkNavigatorProperty); }
            set { SetValue(LinkNavigatorProperty, value); }
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

        public static readonly DependencyProperty AppUpdateAvailableProperty = DependencyProperty.Register(nameof(AppUpdateAvailable), typeof(string),
                typeof(ModernWindow));

        public string AppUpdateAvailable {
            get { return (string)GetValue(AppUpdateAvailableProperty); }
            set { SetValue(AppUpdateAvailableProperty, value); }
        }

        public static readonly DependencyProperty AppUpdateCommandProperty = DependencyProperty.Register(nameof(AppUpdateCommand), typeof(ICommand),
                typeof(ModernWindow));

        public ICommand AppUpdateCommand {
            get { return (ICommand)GetValue(AppUpdateCommandProperty); }
            set { SetValue(AppUpdateCommandProperty, value); }
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernWindow));

        public string SaveKey {
            get { return (string)GetValue(SaveKeyProperty); }
            set { SetValue(SaveKeyProperty, value); }
        }
    }
}
