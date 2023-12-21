using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    [ContentProperty(nameof(AdditionalContent))]
    public class ModernWindow : DpiAwareWindow {
        public static RoutedUICommand NavigateTitleLink { get; } = new RoutedUICommand(UiStrings.NavigateLink, nameof(NavigateTitleLink), typeof(LinkCommands));

        public ModernWindow() {
            DefaultStyleKey = typeof(ModernWindow);

            SetCurrentValue(MenuLinkGroupsProperty, new LinkGroupCollection());
            SetCurrentValue(TitleButtonsProperty, new ObservableCollection<UIElement>());
            SetCurrentValue(TitleLinksProperty, new LinkCollection());

            CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindow, OnCanResizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimizeWindow, OnCanMinimizeWindow));
            CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindow, OnCanResizeWindow));
            CommandBindings.Add(new CommandBinding(LinkCommands.NavigateLink, OnNavigateLink, OnCanNavigateLink));
            CommandBindings.Add(new CommandBinding(NavigateTitleLink, OnNavigateTitleLink, OnCanNavigateTitleLink));
        }

        public event EventHandler<NavigatingCancelEventArgs> FrameNavigating;
        public event EventHandler<NavigationEventArgs> FrameNavigated;

        [CanBeNull]
        private ModernMenu _menu;

        [CanBeNull]
        private ModernFrame _frame;

        [CanBeNull]
        public Uri CurrentSource => _frame?.Source;

        public LinkGroup CurrentLinkGroup => _menu?.SelectedLinkGroup;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_frame != null) {
                _frame.Navigating -= OnFrameNavigating;
                _frame.Navigated -= OnFrameNavigated;
            }

            _frame = GetTemplateChild(@"ContentFrame") as ModernFrame;

            if (_frame != null) {
                _frame.Navigating += OnFrameNavigating;
                _frame.Navigated += OnFrameNavigated;
            }

            _menu = GetTemplateChild(@"PART_Menu") as ModernMenu;
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs navigationEventArgs) {
            FrameNavigated?.Invoke(this, navigationEventArgs);

            var activeKey = _menu?.SelectedLinkGroup?.GroupKey;
            foreach (var link in TitleLinks.OfType<TitleLink>()) {
                link.IsActive = link.GroupKey == activeKey;
            }
        }

        private void OnFrameNavigating(object sender, NavigatingCancelEventArgs navigationEventArgs) {
            FrameNavigating?.Invoke(this, navigationEventArgs);
        }

        private void OnCanNavigateTitleLink(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void OnNavigateTitleLink(object sender, ExecutedRoutedEventArgs e) {
            if (LinkNavigator == null) return;

            var titleLink = e.Parameter as TitleLink;
            if (titleLink?.GroupKey != null && !titleLink.GroupKey.Contains(@"/")) {
                _menu?.SwitchToGroupByKey(titleLink.GroupKey);
                return;
            }

            var eParameter = (e.Parameter as Link)?.Source ?? e.Parameter;
            if (NavigationHelper.TryParseUriWithParameters(eParameter, out var uri, out var parameter, out var _, out _)) {
                LinkNavigator.Navigate(uri, e.Source as FrameworkElement, parameter);
            }
        }

        public string CurrentGroupKey {
            get => _menu?.SelectedLinkGroup?.GroupKey;
            set => _menu?.SwitchToGroupByKey(value);
        }

        public void NavigateTo(Uri uri) {
            if (_menu == null) return;
            if (uri == _menu.SelectedSource) {
                _menu.SelectedSource = new Uri("about:blank");
            }
            _menu.SelectedSource = uri;
        }

        private void OnCanNavigateLink(object sender, CanExecuteRoutedEventArgs e) {
            // true by default
            e.CanExecute = true;
            if (LinkNavigator?.Commands == null) return;

            // in case of command uri, check if ICommand.CanExecute is true
            // TODO: CanNavigate is invoked a lot, which means a lot of parsing. need improvements?
            if (!NavigationHelper.TryParseUriWithParameters(e.Parameter, out var uri, out var parameter, out var _, out _)) {
                return;
            }

            if (LinkNavigator.Commands.TryGetValue(uri, out var command)) {
                e.CanExecute = command.CanExecute(parameter);
            }
        }

        private void OnNavigateLink(object sender, ExecutedRoutedEventArgs e) {
            if (LinkNavigator == null) return;
            if (NavigationHelper.TryParseUriWithParameters(e.Parameter, out var uri, out var parameter, out var _, out _)) {
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

        public static readonly DependencyProperty FrameMarginProperty = DependencyProperty.Register(nameof(FrameMargin), typeof(Thickness),
                typeof(ModernWindow));

        public Thickness FrameMargin {
            get => GetValue(FrameMarginProperty) as Thickness? ?? default;
            set => SetValue(FrameMarginProperty, value);
        }

        public static readonly DependencyProperty BackgroundContentProperty = DependencyProperty.Register(nameof(BackgroundContent), typeof(object),
                typeof(ModernWindow));

        public object BackgroundContent {
            get => GetValue(BackgroundContentProperty); // TODO: Optimize?
            set => SetValue(BackgroundContentProperty, value);
        }

        public static readonly DependencyProperty TitleLinksPrefixProperty = DependencyProperty.Register(nameof(TitleLinksPrefix), typeof(object),
                typeof(ModernWindow), new PropertyMetadata(null, (o, e) => {
                    ((ModernWindow)o)._titleLinksPrefix = e.NewValue;
                }));

        private object _titleLinksPrefix;

        public object TitleLinksPrefix {
            get => _titleLinksPrefix;
            set => SetValue(TitleLinksPrefixProperty, value);
        }

        public static readonly DependencyProperty MenuLinkGroupsProperty = DependencyProperty.Register(nameof(MenuLinkGroups), typeof(LinkGroupCollection),
                typeof(ModernWindow));

        public LinkGroupCollection MenuLinkGroups {
            get => (LinkGroupCollection)GetValue(MenuLinkGroupsProperty);
            set => SetValue(MenuLinkGroupsProperty, value);
        }

        public static readonly DependencyProperty TitleButtonsProperty = DependencyProperty.Register(nameof(TitleButtons),
                typeof(ObservableCollection<UIElement>), typeof(ModernWindow));

        public ObservableCollection<UIElement> TitleButtons {
            get => (ObservableCollection<UIElement>)GetValue(TitleButtonsProperty);
            set => SetValue(TitleButtonsProperty, value);
        }

        public static readonly DependencyProperty TitleLinksProperty = DependencyProperty.Register(nameof(TitleLinks), typeof(LinkCollection),
                typeof(ModernWindow));

        public LinkCollection TitleLinks {
            get => (LinkCollection)GetValue(TitleLinksProperty);
            set => SetValue(TitleLinksProperty, value);
        }

        public static readonly DependencyProperty IsTitleVisibleProperty = DependencyProperty.Register(nameof(IsTitleVisible), typeof(bool),
                typeof(ModernWindow),
                new PropertyMetadata(false));

        public bool IsTitleVisible {
            get => GetValue(IsTitleVisibleProperty) as bool? == true;
            set => SetValue(IsTitleVisibleProperty, value);
        }

        public static readonly DependencyProperty IsRootMarginEvenProperty = DependencyProperty.Register(nameof(IsRootMarginEven), typeof(bool),
                typeof(ModernWindow), new PropertyMetadata(false, (o, e) => {
                    ((ModernWindow)o)._isRootMarginEven = (bool)e.NewValue;
                }));

        private bool _isRootMarginEven;

        public bool IsRootMarginEven {
            get => _isRootMarginEven;
            set => SetValue(IsRootMarginEvenProperty, value);
        }

        public static readonly DependencyProperty DefaultContentSourceProperty = DependencyProperty.Register(nameof(DefaultContentSource), typeof(Uri),
                typeof(ModernWindow));

        public Uri DefaultContentSource {
            get => (Uri)GetValue(DefaultContentSourceProperty);
            set => SetValue(DefaultContentSourceProperty, value);
        }

        public static readonly DependencyProperty ContentLoaderProperty = DependencyProperty.Register(nameof(ContentLoader), typeof(IContentLoader),
                typeof(ModernWindow), new PropertyMetadata(new DefaultContentLoader()));

        public IContentLoader ContentLoader {
            get => (IContentLoader)GetValue(ContentLoaderProperty);
            set => SetValue(ContentLoaderProperty, value);
        }

        public static DependencyProperty LinkNavigatorProperty = DependencyProperty.Register(nameof(LinkNavigator), typeof(ILinkNavigator), typeof(ModernWindow),
                new PropertyMetadata(new DefaultLinkNavigator()));

        public ILinkNavigator LinkNavigator {
            get => (ILinkNavigator)GetValue(LinkNavigatorProperty);
            set => SetValue(LinkNavigatorProperty, value);
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernWindow));

        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        public static readonly DependencyProperty AdditionalContentProperty = DependencyProperty.Register(nameof(AdditionalContent), typeof(object),
                typeof(ModernWindow));

        public object AdditionalContent {
            get => GetValue(AdditionalContentProperty);
            set => SetValue(AdditionalContentProperty, value);
        }

        public static readonly DependencyProperty BackButtonVisibilityProperty = DependencyProperty.Register(nameof(BackButtonVisibility), typeof(Visibility),
                typeof(ModernWindow), new PropertyMetadata(Visibility.Visible));

        public Visibility BackButtonVisibility {
            get => GetValue(BackButtonVisibilityProperty) as Visibility? ?? default;
            set => SetValue(BackButtonVisibilityProperty, value);
        }

        public static readonly DependencyProperty TitleButtonsVisibilityProperty = DependencyProperty.Register(nameof(TitleButtonsVisibility), typeof(Visibility),
                typeof(ModernWindow), new PropertyMetadata(Visibility.Visible));

        public Visibility TitleButtonsVisibility {
            get => (Visibility)GetValue(TitleButtonsVisibilityProperty);
            set => SetValue(TitleButtonsVisibilityProperty, value);
        }

        public static readonly DependencyProperty MenuTopRowVisibilityProperty = DependencyProperty.Register(nameof(MenuTopRowVisibility), typeof(Visibility),
                typeof(ModernWindow), new PropertyMetadata(Visibility.Visible));

        public Visibility MenuTopRowVisibility {
            get => (Visibility)GetValue(MenuTopRowVisibilityProperty);
            set => SetValue(MenuTopRowVisibilityProperty, value);
        }

        public static readonly DependencyProperty ShowErrorsButtonProperty = DependencyProperty.Register(nameof(ShowErrorsButton), typeof(bool),
                typeof(ModernWindow), new PropertyMetadata(true));

        public bool ShowErrorsButton {
            get => (bool)GetValue(ShowErrorsButtonProperty);
            set => SetValue(ShowErrorsButtonProperty, value);
        }

        public static readonly DependencyProperty CloseButtonProperty = DependencyProperty.Register(nameof(CloseButton), typeof(object),
                typeof(ModernWindow));

        public object CloseButton {
            get => GetValue(CloseButtonProperty);
            set => SetValue(CloseButtonProperty, value);
        }
    }
}