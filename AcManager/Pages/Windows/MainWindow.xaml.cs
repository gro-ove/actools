using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.QuickSwitches;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.DiscordRpc;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools;
using AcManager.Tools.About;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcManager.UserControls;
using AcManager.Workshop;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Path = System.Windows.Shapes.Path;
using QuickSwitchesBlock = AcManager.QuickSwitches.QuickSwitchesBlock;

namespace AcManager.Pages.Windows {
    public partial class MainWindow : IFancyBackgroundListener, INavigateUriHandler {
        private static readonly TitleLinkEnabledEntry DownloadsEntry = new TitleLinkEnabledEntry("downloads", AppStrings.Main_Downloads);

        public static TitleLinkEnabledEntry[] GetTitleLinksEntries() {
            return new[] {
                // new TitleLinkEnabledEntry("drive", AppStrings.Main_Drive),
                new TitleLinkEnabledEntry("lapTimes", AppStrings.Main_LapTimes),
                new TitleLinkEnabledEntry("stats", AppStrings.Main_Results),
                new TitleLinkEnabledEntry("media", AppStrings.Main_Media),
                WorkshopClient.OptionUserAvailable ? new TitleLinkEnabledEntry("workshop", AppStrings.Main_Workshop) : null,
                new TitleLinkEnabledEntry("content", AppStrings.Main_Content),
                DownloadsEntry,
                new TitleLinkEnabledEntry("server", AppStrings.Main_Server, false),
                new TitleLinkEnabledEntry("settings", AppStrings.Main_Settings),
                new TitleLinkEnabledEntry("about", AppStrings.Main_About),
                new TitleLinkEnabledEntry("originalLauncher", AppStrings.Windows_MainWindow_OriginalLauncherAppearsWithSteamStarter),
                new TitleLinkEnabledEntry("settings/video", AppStrings.Windows_MainWindow_VideoSettingsFPSCounter, false),
            }.NonNull().ToArray();
        }

        public static readonly Uri OriginalLauncherUrl = new Uri("cmd://originalLauncher");
        public static readonly Uri EnterKeyUrl = new Uri("cmd://enterKey");

        private readonly bool _cancelled;
        private readonly string _testGameDialog = null;

        public MainWindow() {
            Owner = null;

            var app = Application.Current;
            if (app != null) {
                app.MainWindow = this;
            }

            _cancelled = false;

            if (_testGameDialog != null) {
                Logging.Write("Testing mode");
                var ui = new GameDialog();
                ui.ShowDialogAsync().Ignore();
                ((IGameUi)ui).OnResult(JsonConvert.DeserializeObject<Game.Result>(FileUtils.ReadAllText(_testGameDialog)), null);
                _cancelled = true;
            }

            if (_cancelled) {
                Close();
                return;
            }

            InitializeSubGroups();

            var downloadsNavigateCommand = new NavigateCommand(this, new Uri("/Pages/Miscellaneous/DownloadsList.xaml", UriKind.Relative));
            DataContext = new ViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(new NavigateCommand(this, "drive"), new KeyGesture(Key.F1)),
                new InputBinding(new NavigateCommand(this, "lapTimes"), new KeyGesture(Key.F2)),
                new InputBinding(new NavigateCommand(this, "stats"), new KeyGesture(Key.F3)),
                new InputBinding(new NavigateCommand(this, "media"), new KeyGesture(Key.F4)),

                // Second group, Ctrl+F…
                new InputBinding(new NavigateCommand(this, new Uri("/Pages/Lists/CarsListPage.xaml", UriKind.Relative)),
                        new KeyGesture(Key.F1, ModifierKeys.Control)),
                InternalUtils.IsAllRight ? new InputBinding(new NavigateCommand(this, new Uri("/Pages/Lists/ServerPresetsListPage.xaml", UriKind.Relative)),
                        new KeyGesture(Key.F2, ModifierKeys.Control)) : null,

                // Downloads hotkey
                new InputBinding(new DelegateCommand(() => {
                    if (AppAppearanceManager.Instance.DownloadsInSeparatePage) {
                        downloadsNavigateCommand.Execute();
                    } else {
                        this.RequireChild<Popup>("DownloadsPopup").IsOpen = true;
                    }
                }), new KeyGesture(Key.J, ModifierKeys.Control)),

                // Settings, Alt+F…
                new InputBinding(new NavigateCommand(this, new Uri("/Pages/Settings/SettingsPage.xaml", UriKind.Relative)),
                        new KeyGesture(Key.F1, ModifierKeys.Alt)),
                new InputBinding(new NavigateCommand(this, new Uri("/Pages/AcSettings/AcSettingsPage.xaml", UriKind.Relative)),
                        new KeyGesture(Key.F2, ModifierKeys.Alt)),
                new InputBinding(new NavigateCommand(this, new Uri("/Pages/Settings/PythonAppsSettings.xaml", UriKind.Relative)),
                        new KeyGesture(Key.F3, ModifierKeys.Alt)),
            }.NonNull().ToList());

            InitializeComponent();
            RaceU.InitializeRaceULinks();
            ModsWebBrowser.Instance.RebuildLinksNow();
            ArgumentsHandler.HandlePasteEvent(this);

            if (!WorkshopClient.OptionUserAvailable) {
                foreach (var link in TitleLinks.OfType<TitleLink>().Where(x => x.GroupKey == "workshop").ToList()) {
                    TitleLinks.Remove(link);
                }
                foreach (var link in MenuLinkGroups.Where(x => x.GroupKey == "workshop").ToList()) {
                    MenuLinkGroups.Remove(link);
                }
            }

            if (SteamStarter.IsInitialized) {
                OverlayContentCell.Children.Add((FrameworkElement)FindResource(@"SteamOverlayFix"));
            }

            LinkNavigator.Commands.Add(new Uri("cmd://enterKey"), Model.EnterKeyCommand);
            if (SettingsHolder.Drive.SelectedStarterType != SettingsHolder.DriveSettings.SteamStarterType) {
                TitleLinks.Remove(OriginalLauncher);
            } else {
                LinkNavigator.Commands.Add(new Uri("cmd://originalLauncher"), new DelegateCommand(SteamStarter.StartOriginalLauncher));
            }

            InternalUtils.Launch(this);

            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                    .Where(x => x.Source.OriginalString.Contains(@"/online.xaml", StringComparison.OrdinalIgnoreCase))) {
                result.LinkChanged += OnlineLinkChanged;
            }

            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                    .Where(x => x.Source.OriginalString.Contains(@"/laptimes_table.xaml", StringComparison.OrdinalIgnoreCase))) {
                result.LinkChanged += LapTimesLinkChanged;
            }

            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                    .Where(x => x.GroupKey == "media" || x.GroupKey == "content")) {
                result.LinkChanged += ContentLinkChanged;
            }

            UpdateLiveTabs();
            SettingsHolder.Live.PropertyChanged += OnLiveSettingsPropertyChanged;

            UpdateTitleLinks();
            AppAppearanceManager.Instance.PropertyChanged += OnAppAppearancePropertyChanged;

            UpdateMinoratingLink();
            SettingsHolder.Online.PropertyChanged += OnOnlineSettingsPropertyChanged;

            _defaultOnlineGroupCount = OnlineGroup.FixedLinks.Count;

            if (FileBasedOnlineSources.IsInitialized()) {
                UpdateOnlineSourcesLinks();
            }

            FileBasedOnlineSources.Instance.Update += OnOnlineSourcesUpdate;
            if (CupClient.Instance != null) CupClient.Instance.NewLatestVersion += OnNewLatestVersion;
            Activated += OnActivated;

            ContentInstallationManager.Instance.TaskAdded += OnContentInstallationTaskAdded;
            UpdateDiscordRichPresence();

#if DEBUG
            // LapTimesGrid.Source = new Uri("/Pages/Miscellaneous/LapTimes_Grid.xaml", UriKind.Relative);
#endif
        }

        private static Uri _navigateOnOpen;

        public static void NavigateOnOpen(Uri uri) {
            _navigateOnOpen = uri;
        }

        protected override void OnLoadedOverride() {
            if (_navigateOnOpen != null) {
                NavigateTo(_navigateOnOpen);
                this.FindVisualChild<ModernMenu>()?.SkipLoading();
                _navigateOnOpen = null;
            }

            // SettingsShadersPatch.GetShowSettingsCommand().Execute(null);
        }

        private readonly Busy _openDownloadsListBusy = new Busy();

        private void OnContentInstallationTaskAdded(object o, EventArgs eventArgs) {
            _openDownloadsListBusy.Yield(() => {
                if (IsVisible && !VisualExtension.IsInputFocused()
                        && AppAppearanceManager.Instance.DownloadsInSeparatePage
                        && AppAppearanceManager.Instance.DownloadsPageAutoOpen) {
                    NavigateTo(new Uri("/Pages/Miscellaneous/DownloadsList.xaml", UriKind.Relative));
                }
            });

            if (!AppAppearanceManager.Instance.DownloadsInSeparatePage) {
                FancyHints.DownloadsList.Trigger();
            }
        }

        private void OnActivated(object sender, EventArgs e) {
            Activated -= OnActivated;

            var app = Application.Current;
            if (app == null) return;

            foreach (var dialog in app.Windows.OfType<ModernDialog>().ToList()) {
                if (dialog.Owner != null) continue;
                try {
                    dialog.Owner = this;
                } catch (Exception ex) {
                    Logging.Warning(ex.Message);
                }
            }
        }

        private readonly int _defaultOnlineGroupCount;

        private void UpdateOnlineSourcesLinks() {
            var list = OnlineGroup.FixedLinks;

            for (var i = list.Count - 1; i >= _defaultOnlineGroupCount; i--) {
                list.RemoveAt(i);
            }

            foreach (var source in FileBasedOnlineSources.Instance.GetVisibleSources().OrderBy(x => x.DisplayName)) {
                list.Add(new Link {
                    DisplayName = $@"{source.DisplayName}",
                    Source = UriExtension.Create("/Pages/Drive/Online.xaml?Filter=@{0}&Special=1", source.Id)
                });
            }
        }

        public void UpdateRaceULinks(IEnumerable<Link> links) {
            for (var i = RaceUGroup.Links.Count - 1; i > 0; --i) {
                RaceUGroup.Links.RemoveAt(i);
            }
            foreach (var link in links) {
                RaceUGroup.Links.Add(link);
            }
        }

        private void OnOnlineSourcesUpdate(object sender, EventArgs e) {
            UpdateOnlineSourcesLinks();
        }

        private void OnOnlineSettingsPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.OnlineSettings.IntegrateMinorating):
                    UpdateMinoratingLink();
                    break;
            }
        }

        private void UpdateTitleLinks() {
            var value = AppAppearanceManager.Instance.DownloadsInSeparatePage;
            DownloadsEntry.IsAvailable = value;
            BrowserLinkGroup.GroupKey = value ? @"downloads" : @"content";
            TitleLinks.OfType<TitleLink>().Where(x => x.GroupKey != null)
                    .ForEach(x => x.IsShown = AppAppearanceManager.Instance.IsTitleLinkVisible(x.GroupKey) != false);
            AppAppearanceManager.Instance.TitleLinkEntries.ForEach(x => x.PropertyChanged += OnTitleLinkEnabledChanged);
        }

        private Border _downloadListParent;

        private void OnDownloadListParentLoaded(object sender, RoutedEventArgs e) {
            if (!AppAppearanceManager.Instance.DownloadsInSeparatePage) {
                _downloadListParent = (Border)sender;
                if (_downloadListParent.Child == null) {
                    _downloadListParent.Child = (FrameworkElement)FindResource(@"DownloadsMenuSection");
                    _downloadsListSet = false;
                }
            }
        }

        private void OnAppAppearancePropertyChanged(object o, PropertyChangedEventArgs args) {
            if (args.PropertyName == nameof(AppAppearanceManager.Instance.DownloadsInSeparatePage)) {
                var value = AppAppearanceManager.Instance.DownloadsInSeparatePage;
                if (_downloadListParent != null) {
                    _downloadListParent.Child = value ? null : (FrameworkElement)FindResource(@"DownloadsMenuSection");
                }
                DownloadsEntry.IsAvailable = value;
                BrowserLinkGroup.GroupKey = value ? @"downloads" : @"content";
                MenuLinkGroups = new LinkGroupCollection(MenuLinkGroups.ToList());
            }
        }

        private void OnTitleLinkEnabledChanged(object o, PropertyChangedEventArgs args) {
            var entry = (TitleLinkEnabledEntry)o;
            var link = TitleLinks.OfType<TitleLink>().FirstOrDefault(x => x.GroupKey == entry.Id);
            if (link != null) {
                link.IsShown = entry.IsEnabled && entry.IsAvailable;
            }
        }

        private void UpdateMinoratingLink() {
            // MinoratingLink.IsShown = SettingsHolder.Online.IntegrateMinorating;
        }

        private void OnLiveSettingsPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.LiveSettings.RsrEnabled) ||
                    e.PropertyName == nameof(SettingsHolder.LiveSettings.SrsEnabled)) {
                UpdateLiveTabs();
            }
        }

        private void UpdateLiveTabs() {
            RsrLink.IsShown = SettingsHolder.Live.RsrEnabled;
            // SrsLink.IsShown = SettingsHolder.Live.SrsEnabled;
            Srs2Link.IsShown = SettingsHolder.Live.SrsEnabled;
            WorldSimSeriesLink.IsShown = SettingsHolder.Live.WorldSimSeriesEnabled;
            TrackTitanLink.IsShown = SettingsHolder.Live.TrackTitanEnabled;
            LiveGroup.IsShown = LiveGroup.Links.Any(x => x.IsShown);
            // ShortSurveyLink.IsShown = !Stored.Get<bool>("surveyHide").Value;

            RaceUGroup.IsShown = SettingsHolder.Live.RaceUEnabled && (ValuesStorage.Contains("RaceU.CurrentLocation") || RaceUCheckAb());

            bool RaceUCheckAb() {
                var steamId = SteamIdHelper.Instance.Value;
                if (steamId == null) return false;

                using (var algo = MD5.Create()) {
                    return BitConverter.ToInt32(algo.ComputeHash(Encoding.UTF8.GetBytes(steamId)), 0) % 10 < 4;
                }
            }
        }

        /// <summary>
        /// Temporary (?) fix.
        /// </summary>
        private static void OnlineLinkChanged(object sender, LinkChangedEventArgs e) {
            Online.OnLinkChanged(e);
        }

        /// <summary>
        /// Temporary (?) fix.
        /// </summary>
        private static void LapTimesLinkChanged(object sender, LinkChangedEventArgs e) {
            LapTimes_Table.OnLinkChanged(e);
        }

        /// <summary>
        /// Temporary (?) fix to keep selected object while editing current filter.
        /// </summary>
        private static void ContentLinkChanged(object sender, LinkChangedEventArgs e) {
            switch (((LinkGroupFilterable)sender).Source.GetName()) {
                case nameof(ReplaysListPage):
                    AcListPageViewModel<ReplayObject>.OnLinkChanged(e);
                    break;
                case nameof(CarsListPage):
                    AcListPageViewModel<CarObject>.OnLinkChanged(e);
                    break;
                case nameof(TracksListPage):
                    AcListPageViewModel<TrackObject>.OnLinkChanged(e);
                    break;
                case nameof(ShowroomsListPage):
                    AcListPageViewModel<ShowroomObject>.OnLinkChanged(e);
                    break;
            }
        }

        private class NavigateCommand : CommandExt {
            private readonly MainWindow _window;
            private readonly Uri _uri;
            private readonly string _key;

            public NavigateCommand(MainWindow window, [Localizable(false)] string key) : base(true, false) {
                _window = window;
                _key = key;
            }

            public NavigateCommand(MainWindow window, Uri uri) : base(true, false) {
                if (VisualExtension.IsInputFocused() && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
                _window = window;
                _uri = uri;
            }

            protected override bool CanExecuteOverride() {
                return true;
            }

            protected override void ExecuteOverride() {
                if (_uri != null) {
                    _window.NavigateTo(_uri);
                    return;
                }

                var link = _window.TitleLinks.OfType<TitleLink>().FirstOrDefault(x => x.GroupKey == _key);
                if (link == null || !link.IsEnabled || link.NonSelectable) return;
                _window.NavigateTo(link.Source);
            }
        }

        public new void Show() {
            if (_cancelled) {
                Logging.Write("Cancelled");
                return;
            }

            base.Show();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private CommandBase _enterKeyCommand;

            public ICommand EnterKeyCommand => _enterKeyCommand ?? (_enterKeyCommand = new DelegateCommand(() => { new AppKeyDialog().ShowDialog(); }));

            private DelegateCommand _showAdditionalContentDialogCommand;

            public DelegateCommand ShowAdditionalContentDialogCommand => _showAdditionalContentDialogCommand ??
                    (_showAdditionalContentDialogCommand = new DelegateCommand(InstallAdditionalContentDialog.ShowInstallDialog));

            public AppUpdater AppUpdater => AppUpdater.Instance;

            private AsyncCommand _viewChangelogCommand;

            public AsyncCommand ViewChangelogCommand => _viewChangelogCommand ?? (_viewChangelogCommand = new AsyncCommand(async () => {
                List<ChangelogEntry> changelog;
                try {
                    changelog = await Task.Run(() =>
                            AppUpdater.LoadChangelog().Where(x => x.Version.IsVersionNewerThan(AppUpdater.PreviousVersion)).ToList());
                } catch (WebException e) {
                    NonfatalError.NotifyBackground(AppStrings.Changelog_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks, e);
                    return;
                } catch (Exception e) {
                    NonfatalError.NotifyBackground(AppStrings.Changelog_CannotLoad, e);
                    return;
                }

                Logging.Debug("Changelog entries: " + changelog.Count);
                if (changelog.Any()) {
                    ModernDialog.ShowMessage(changelog.Select(x => $@"[b]{x.Version}[/b]{Environment.NewLine}{x.Changes}")
                            .JoinToString(Environment.NewLine.RepeatString(2)), AppStrings.Changelog_RecentChanges_Title,
                            MessageBoxButton.OK);
                } else {
                    ModernDialog.ShowMessage(AppStrings.AdditionalContent_NothingFound.ToSentence(), AppStrings.Changelog_RecentChanges_Title,
                            MessageBoxButton.OK);
                }
            }));
        }

        void IFancyBackgroundListener.ChangeBackground(string filename) {
            if (_dynamicBackground != null) return;
            var backgroundContent = BackgroundContent;
            FancyBackgroundManager.UpdateBackground(this, ref backgroundContent);
            if (!ReferenceEquals(backgroundContent, BackgroundContent)) {
                BackgroundContent = backgroundContent;
            }
        }

        private HwndSourceHook _hook;
        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AboutHelper.Instance.PropertyChanged += OnAboutPropertyChanged;
            UpdateAboutIsNew();

            var background = AppArguments.Get(AppFlag.Background);
            if (string.IsNullOrWhiteSpace(background)) {
                FancyBackgroundManager.Instance.AddListener(this);
                SetThemeDynamicBackgroundListener();
            } else {
                background = FileUtils.GetFullPath(background, () => FilesStorage.Instance.GetDirectory("Themes", "Backgrounds"));
                ApplyDynamicBackground(background, AppArguments.GetDouble(AppFlag.BackgroundOpacity, 0.5));
            }

            if (CupViewModel.Instance.ToUpdate.Count > 0) {
                FancyHints.ContentUpdatesArrived.Trigger();
            } else {
                CupViewModel.Instance.NewUpdate += (o, args) => FancyHints.ContentUpdatesArrived.Trigger();
            }
            Logging.Debug("Main window is loaded and ready");
        }

        private DynamicBackground _dynamicBackground;

        private void ApplyDynamicBackground([CanBeNull] string filename, double opacity = 0.5) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                try {
                    if (filename == null) {
                        DisposeHelper.Dispose(ref _dynamicBackground);
                        if (FancyBackgroundManager.Instance.Enabled) {
                            FancyBackgroundManager.Instance.Recreate(this);
                        } else {
                            ClearValue(BackgroundContentProperty);
                        }
                    } else {
                        var animatedBackground = Regex.IsMatch(filename, @"\.(?:avi|flv|gif|m(?:4[pv]|kv|ov|p[4g])|og[vg]|qt|webm|wmv)$",
                                RegexOptions.IgnoreCase) ?
                                filename : null;
                        var staticBackground = animatedBackground == null ? filename : Regex.Replace(filename, @"\.\w+$", @".jpg");

                        _dynamicBackground?.Dispose();
                        BackgroundContent = _dynamicBackground = new DynamicBackground {
                            Animated = animatedBackground,
                            Static = staticBackground,
                            Opacity = opacity
                        };
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            });
        }

        private void UpdateThemeDynamicBackground() {
            if (AppearanceManager.Instance.CurrentThemeDictionary?[@"DynamicBackground"] is string value) {
                value = FileUtils.GetFullPath(value, () => FilesStorage.Instance.GetDirectory());
                ApplyDynamicBackground(value, AppearanceManager.Instance.CurrentThemeDictionary?[@"DynamicBackgroundOpacity"] as double? ?? 0.5);
            } else {
                ApplyDynamicBackground(null);
            }
        }

        private void SetThemeDynamicBackgroundListener() {
            UpdateThemeDynamicBackground();
            AppearanceManager.Instance.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(AppearanceManager.CurrentThemeDictionary)) {
                    ((Action)UpdateThemeDynamicBackground).InvokeInMainThreadAsync();
                }
            };
        }

        private void UpdateAboutIsNew() {
            TitleLinks.FirstOrDefault(x => x.DisplayName == AppStrings.Main_About)?
                    .SetNew(AboutHelper.Instance.HasNewImportantTips || AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                    .FirstOrDefault(x => x.DisplayName == AppStrings.Main_ReleaseNotes)?
                    .SetNew(AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                    .FirstOrDefault(x => x.DisplayName == AppStrings.Main_ImportantTips)?
                    .SetNew(AboutHelper.Instance.HasNewImportantTips);
        }

        private void OnAboutPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AboutHelper.HasNewReleaseNotes) || e.PropertyName == nameof(AboutHelper.HasNewImportantTips)) {
                UpdateAboutIsNew();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            FancyBackgroundManager.Instance.RemoveListener(this);
            AboutHelper.Instance.PropertyChanged -= OnAboutPropertyChanged;

            if (_hook == null) return;

            try {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.RemoveHook(_hook);
            } catch (Exception) {
                Logging.Warning("Can’t remove one-instance hook");
            }

            _hook = null;
        }

        private void OnDrop(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDrop(sender, e);
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDragEnter(e);
        }

        private void OnClosed(object sender, EventArgs e) {
            _dynamicBackground?.Dispose();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                WindowsHelper.RestartCurrentApplication();
            } else {
                var app = Application.Current;
                if (app == null) {
                    Environment.Exit(0);
                } else {
                    app.Shutdown();
                }
            }
        }

        private bool _closed;

        private void OnClosing(object sender, CancelEventArgs e) {
            if (_closed) return;

            try {
                if (ServerPresetsManager.Instance.IsScanned) {
                    var running = ServerPresetsManager.Instance.Loaded.Where(x => x.IsRunning).ToList();
                    if (running.Count > 0 && ModernDialog.ShowMessage(
                            $@"{"If you’ll close app, running servers will be stopped as well. Are you sure?"}{Environment.NewLine}{Environment.NewLine}{
                                    running.Select(x => $@" • {BbCodeBlock.Encode(x.DisplayName)}").JoinToString(Environment.NewLine)}",
                            "Some servers are running", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                        e.Cancel = true;
                        return;
                    }
                }

                if (ContentInstallationManager.Instance.HasUnfinishedItems && ModernDialog.ShowMessage(
                        "If you’ll close app, installation will be terminated. Are you sure?",
                        "Something is being installed", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    e.Cancel = true;
                    return;
                }

                var unsaved = Superintendent.Instance.UnsavedChanges();
                if (unsaved.Count > 0) {
                    switch (ModernDialog.ShowMessage(
                            $@"{AppStrings.Main_UnsavedChanges}{Environment.NewLine}{Environment.NewLine}{
                                    unsaved.OrderBy(x => x).Select(x => $@" • {x}").JoinToString(Environment.NewLine)}",
                            AppStrings.Main_UnsavedChangesHeader, MessageBoxButton.YesNoCancel)) {
                                case MessageBoxResult.Yes:
                                    Superintendent.Instance.SaveAll();
                                    break;
                                case MessageBoxResult.Cancel:
                                    e.Cancel = true;
                                    return;
                            }
                }

                // Just in case, temporary
                _closed = true;
                Application.Current.Shutdown();
            } catch (Exception ex) {
                Logging.Warning(ex);
            }
        }

        private void InitializePopup() {
            if (Popup.Child == null) {
                Popup.Child = new QuickSwitchesBlock();
            }
        }

        public IEnumerable<FrameworkElement> GetQuickSwitches() {
            InitializePopup();
            return ((QuickSwitchesBlock)Popup.Child).Items;
        }

        private void ToggleQuickSwitches(bool force = true) {
            if (QuickSwitchesBlock.GetIsActive(Popup)) {
                QuickSwitchesBlock.SetIsActive(Popup, false);
            } else if (force || _openOnNext) {
                InitializePopup();
                QuickSwitchesBlock.SetIsActive(Popup, true);
                Popup.Focus();
            }
        }

        public void CloseQuickSwitches() {
            QuickSwitchesBlock.SetIsActive(Popup, false);
        }

        private ICommand _quickSwitchesCommand;

        public ICommand QuickSwitchesCommand => _quickSwitchesCommand ?? (_quickSwitchesCommand = new DelegateCommand(() => { ToggleQuickSwitches(); }));

        private int _popupId;

        private async void ShowQuickSwitchesPopup(Geometry icon, string message, object toolTip) {
            if (QuickSwitchesBlock.GetIsActive(Popup)) return;

            var id = ++_popupId;
            QuickSwitchesNotificationIcon.Data = icon;
            QuickSwitchesNotificationText.Text = message?.ToUpper(CultureInfo.CurrentUICulture);
            QuickSwitchesNotification.IsOpen = true;
            QuickSwitchesNotification.ToolTip = toolTip;

            await Task.Delay(2000);

            if (_popupId == id) {
                QuickSwitchesNotification.IsOpen = false;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers != ModifierKeys.Alt && Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Shift) ||
                    !SettingsHolder.Drive.QuickSwitches || VisualExtension.IsInputFocused()) return;

            switch (e.SystemKey) {
                case Key.OemTilde:
                    ToggleQuickSwitches();
                    break;

                default:
                    var k = e.SystemKey - Key.D1;
                    if (k < 0 || k > 9) return;

                    InitializePopup();
                    var child = GetQuickSwitches().ElementAtOrDefault(k);
                    if (child == null) break;

                    QuickSwitchesNotification.SetValue(TextBlock.ForegroundProperty, child.GetValue(TextBlock.ForegroundProperty));

                    if (child is ModernToggleButton toggle) {
                        toggle.IsChecked = !toggle.IsChecked;
                        ShowQuickSwitchesPopup(toggle.IconData, $@"{toggle.Content}: {toggle.IsChecked.ToReadableBoolean()}", child.ToolTip);
                        break;
                    }

                    if (child is ModernButton button) {
                        button.Command?.Execute(null);
                        ShowQuickSwitchesPopup(button.IconData, button.Content?.ToString(), child.ToolTip);
                        break;
                    }

                    if (child is QuickSwitchPresetsControl presets) {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                            presets.SwitchToPrevious();
                        } else {
                            presets.SwitchToNext();
                        }
                        ShowQuickSwitchesPopup(presets.IconData, $@"{presets.CurrentUserPreset.DisplayName}", child.ToolTip);
                        break;
                    }

                    if (child is QuickSwitchComboBox combo && combo.Items.Count > 1) {
                        var index = combo.SelectedIndex;
                        combo.SelectedItem = combo.Items[(index + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) +
                                combo.Items.Count) % combo.Items.Count];
                        ShowQuickSwitchesPopup(combo.IconData, $@"{combo.SelectedItem}", child.ToolTip);
                        break;
                    }

                    if (child is QuickSwitchSlider slider) {
                        var step = (slider.Maximum - slider.Minimum) / 6d;
                        var position = (((slider.Value - slider.Minimum) / step - 1).Clamp(0, 4).Round() +
                                (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) + 5) % 5;
                        slider.Value = slider.Minimum + (position + 1) * step;
                        ShowQuickSwitchesPopup(slider.IconData, $@"{slider.Content}: {slider.DisplayValue}", child.ToolTip);
                    }

                    // special case for controls presets
                    if (child is DockPanel dock) {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                            ControlsPresets.Instance.SwitchToPrevious();
                        } else {
                            ControlsPresets.Instance.SwitchToNext();
                        }

                        ShowQuickSwitchesPopup(dock.FindLogicalChild<Path>()?.Data, $@"{AcSettingsHolder.Controls.CurrentPresetName}", child.ToolTip);
                    }

                    break;
            }

            e.Handled = true;
        }

        private async void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Drive.QuickSwitches || !SettingsHolder.Drive.QuickSwitchesRightMouseButton) return;

            await Task.Delay(50);
            if (e.Handled) return;

            ToggleQuickSwitches(false);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.Handled || !SettingsHolder.Drive.QuickSwitches) return;
            if (e.ChangedButton == MouseButton.Middle) {
                ToggleQuickSwitches();
                e.Handled = true;
            }
        }

        private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (QuickSwitchesBlock.GetIsActive(Popup)) {
                e.Handled = true;
            }
        }

        private bool _openOnNext;

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            _openOnNext = !QuickSwitchesBlock.GetIsActive(Popup);
        }

        private void OnContentTitleLinkDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(TrackObjectBase.DraggableFormat) is TrackObjectBase trackObject) {
                TracksListPage.Show(trackObject);
            } else if (e.Data.GetData(PythonAppObject.DraggableFormat) is PythonAppObject appObject) {
                PythonAppsListPage.Show(appObject);
            } else if (e.Data.GetData(RaceGridEntry.DraggableFormat) is RaceGridEntry raceGridEntry) {
                CarsListPage.Show(raceGridEntry.Car, raceGridEntry.CarSkin?.Id);
            } else if (e.Data.GetData(CarObject.DraggableFormat) is CarObject carObject) {
                CarsListPage.Show(carObject);
            } else {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            FancyHints.DragForContentSection.MaskAsUnnecessary();
        }

        private void OnDriveTitleLinkDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;
            var trackObject = e.Data.GetData(TrackObjectBase.DraggableFormat) as TrackObject;
            var weatherObject = e.Data.GetData(WeatherObject.DraggableFormat) as WeatherObject;
            var carSkinObject = e.Data.GetData(CarSkinObject.DraggableFormat) as CarSkinObject;

            if (carSkinObject != null) {
                carObject = CarsManager.Instance.GetById(carSkinObject.CarId);
            }

            if (raceGridEntry != null || carObject != null) {
                QuickDrive.Show(carObject ?? raceGridEntry.Car, carSkinObject?.Id ?? raceGridEntry?.CarSkin?.Id);
            } else if (trackObject != null) {
                QuickDrive.Show(track: trackObject);
            } else if (weatherObject != null) {
                QuickDrive.Show(weatherId: weatherObject.Id);
            } else {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            FancyHints.DragForContentSection.MaskAsUnnecessary();
        }

        private static void MakeSureOnlineIsReady([CanBeNull] Uri uri) {
            if (uri?.OriginalString.Contains(@"/online.xaml", StringComparison.OrdinalIgnoreCase) == true) {
                OnlineManager.EnsureInitialized();
            }
        }

        private DiscordRichPresence _discordPresence = new DiscordRichPresence(1, "Preparing to race", "Somewhere in menus").Default();

        // In general, that information should be provided by webpages directly, but just in case some of
        // them will fail to do that, here are some lower-priority messages.
        [Localizable(false)]
        private void UpdateDiscordRichPresence() {
            string details;
            switch (CurrentGroupKey) {
                case "drive":
                    details = "Preparing to a race";
                    break;
                case "lapTimes":
                    details = "Lap times";
                    break;
                case "stats":
                    details = "Stats";
                    break;
                case "media":
                    details = "Replays";
                    break;
                case "content":
                    details = "Sorting out content";
                    break;
                case "server":
                    details = "Servers settings";
                    break;
                case "settings":
                    details = "In settings";
                    break;
                case "about":
                    details = "Reading documentation";
                    break;
                default:
                    details = "Somewhere in menus";
                    break;
            }

            _discordPresence.Details = details;
        }

        private void OnFrameNavigating(object sender, NavigatingCancelEventArgs e) {
            if (e.Source?.OriginalString.IsWebUrl() == true) {
                WindowsHelper.ViewInBrowser(e.Source.OriginalString);
                e.Cancel = true;
                return;
            }

            if (e.Source?.OriginalString.Contains(@"/Pages/About/") == true) {
                _lastAboutSection.Value = e.Source;
            }
            MakeSureOnlineIsReady(e.Source);
            UpdateDiscordRichPresence();
        }

        private void OnMainMenuInitialize(object sender, ModernMenu.InitializeEventArgs e) {
            MakeSureOnlineIsReady(e.LoadedUri);
        }

        private void OnMainMenuSelectedChange(object sender, ModernMenu.SelectedChangeEventArgs e) { }

        private static readonly Uri AboutPageUri = new Uri("/Pages/About/AboutPage.xaml", UriKind.Relative);
        private readonly StoredValue<Uri> _lastAboutSection = Stored.Get("MainWindow.AboutSection", AboutPageUri);

        [Localizable(false)]
        bool INavigateUriHandler.NavigateTo(Uri uri) {
            Logging.Debug(uri);

            var s = uri.ToString();
            if (s.Contains("/Pages/AcSettings/") && !s.Contains("/Pages/AcSettings/AcSettingsPage.xaml")) {
                CurrentGroupKey = "settings";
                NavigateTo(UriExtension.Create("/Pages/AcSettings/AcSettingsPage.xaml?Uri={0}", uri));
                return true;
            }

            if (s.Contains("/Pages/Settings/PythonAppsSettings.xaml")) {
                CurrentGroupKey = "settings";
                NavigateTo(uri);
                return true;
            }

            if (s.Contains("/Pages/Settings/SettingsShadersPatch.xaml")) {
                CurrentGroupKey = "settings";
                NavigateTo(uri);
                return true;
            }

            if (s.Contains("/Pages/Settings/") && !s.Contains("/Pages/Settings/SettingsPage.xaml")) {
                CurrentGroupKey = "settings";
                NavigateTo(UriExtension.Create("/Pages/Settings/SettingsPage.xaml?Uri={0}", uri));
                return true;
            }

            if (s.Contains("/Pages/About/ImportantTipsPage.xaml")) {
                CurrentGroupKey = "about";
                NavigateTo(uri);
                return true;
            }

            return false;
        }

        private const string KeySubGroupKeys = "MainWindow.SubGroups";

        private static string GetSubGroupLinksKey(string groupKey) {
            return $@"MainWindow.SubGroup:{groupKey}";
        }

        private List<string> _subGroupKeys;

        private void InitializeSubGroups() {
            _subGroupKeys = ValuesStorage.GetStringList(KeySubGroupKeys).ToList();
            foreach (var groupKey in _subGroupKeys) {
                foreach (var p in ValuesStorage.GetStringList(GetSubGroupLinksKey(groupKey))) {
                    var v = Storage.DecodeList(p).ToList();
                    if (v.Count != 2) continue;

                    MenuLinkGroups.Add(new LinkGroupFilterable {
                        DisplayName = v[0],
                        GroupKey = groupKey,
                        Source = new Uri(v[1], UriKind.RelativeOrAbsolute)
                    });
                }
            }
        }

        public LinkGroupFilterable OpenSubGroup(string groupKey, string displayName, Uri uri, Lazy<string> filterHint, int limit = 2) {
            var groupLinks = MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == groupKey).ToList();
            var existingLink = groupLinks.FirstOrDefault(x => x.Source == uri);
            if (existingLink == null) {
                existingLink = new LinkGroupFilterable {
                    DisplayName = displayName,
                    GroupKey = groupKey,
                    Source = uri,
                    FilterHint = filterHint
                };

                while (groupLinks.Count >= limit) {
                    MenuLinkGroups.Remove(groupLinks[0]);
                    groupLinks.RemoveAt(0);
                }

                groupLinks.Add(existingLink);
                MenuLinkGroups.Add(existingLink);

                if (!_subGroupKeys.Contains(groupKey)) {
                    _subGroupKeys.Add(groupKey);
                    ValuesStorage.Storage.SetStringList(KeySubGroupKeys, _subGroupKeys);
                }

                ValuesStorage.Storage.SetStringList(GetSubGroupLinksKey(groupKey),
                        groupLinks.Select(x => Storage.EncodeList(x.DisplayName, x.Source.OriginalString)));
            }

            NavigateTo(uri);
            return existingLink;
        }

        private bool _downloadsListSet;

        private void OnDownloadsPopupOpened(object sender, EventArgs e) {
            if (_downloadsListSet) return;
            var popup = (ModernPopup)sender;
            var parent = VisualTreeHelperEx.FindVisualChildren<Border>((FrameworkElement)popup.Content)
                    .FirstOrDefault(x => x.Tag as string == @"DownloadsParent");
            if (parent != null && parent.Child == null) {
                parent.Child = new InstallAdditionalContentList();
                _downloadsListSet = true;
            }
        }

        private void OnDownloadSomethingItemClick(object sender, RoutedEventArgs e) {
            string defaultUrl = null;
            try {
                defaultUrl = Clipboard.GetText().Trim('"');
            } catch {
                // ignored
            }
            var url = Prompt.Show(AppStrings.MainWindow_AddDownload_Message, AppStrings.MainWindow_AddDownload_Title,
                    defaultUrl.IsAnyUrl() ? defaultUrl : null, @"https://…", required: true,
                    comment: AppStrings.MainWindow_AddDownload_Comment);
            if (url != null) {
                ContentInstallationManager.Instance.InstallAsync(url, new ContentInstallationParams(false));
            }
        }

        private void OnInstallFromAFileItemClick(object sender, RoutedEventArgs e) {
            string defaultFilename = null;

            try {
                if (Clipboard.ContainsData(DataFormats.FileDrop)) {
                    defaultFilename = Clipboard.GetFileDropList().OfType<string>().FirstOrDefault();
                } else if (Clipboard.ContainsData(DataFormats.UnicodeText)) {
                    defaultFilename = Clipboard.GetText().Trim('"');
                }
            } catch {
                // ignored
            }

            try {
                if (!File.Exists(defaultFilename)) {
                    defaultFilename = null;
                }
            } catch {
                defaultFilename = null;
            }

            var filename = FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = defaultFilename != null ? null : "installfromafile",
                Filters = {
                    DialogFilterPiece.Archives,
                    DialogFilterPiece.AllFiles
                },
                Title = "Select an archive to install mods from",
                InitialDirectory = FileUtils.GetDirectoryNameSafe(defaultFilename) ?? Shell32.GetPath(KnownFolder.Downloads),
                DefaultFileName = FileUtils.GetFileNameSafe(defaultFilename),
                CheckFileExists = true,
                CustomPlaces = {
                    new FileDialogCustomPlace(Shell32.GetPath(KnownFolder.Downloads))
                }
            });

            if (filename != null) {
                ContentInstallationManager.Instance.InstallAsync(filename, new ContentInstallationParams(false));
            }
        }

        private void OnNavigateItemClick(object sender, RoutedEventArgs e) {
            var item = (MenuItem)sender;
            DownloadsPopup.IsOpen = false;
            NavigateTo(new Uri((string)item.CommandParameter, UriKind.Relative));
        }

        private void OnNavigateAboutItemClick(object sender, RoutedEventArgs e) {
            DownloadsPopup.IsOpen = false;
            NavigateTo(_lastAboutSection.Value ?? AboutPageUri);
        }

        private async void OnCompleted(object sender, EventArgs e) {
            var s = (Storyboard)sender;
            await Task.Yield();
            s.Stop();
            s.Begin();
        }

        private Border _cupNotificationPanel;

        private void OnCupNotificationPanelLoaded(object sender, RoutedEventArgs e) {
            _cupNotificationPanel = (Border)sender;
            if (_cupNotificationPanel.Child == null) {
                var child = (FrameworkElement)FindResource(@"CupNotificationBlock");
                _cupNotificationPanel.Child = child;
            }
        }

        private async void OnNewLatestVersion(object sender, CupEventArgs e) {
            var manager = CupClient.Instance?.GetAssociatedManager(e.Key.Type);
            if (manager == null) return;
            if (await manager.GetObjectByIdAsync(e.Key.Id) is ICupSupportedObject obj && obj.IsCupUpdateAvailable) {
                FancyHints.ContentUpdatesArrived.Trigger();
            }
        }

        private void OnDownloadsButtonClick(object sender, MouseButtonEventArgs e) {
            var glow = this.FindChild<FrameworkElement>("UpdateMarkGlow");
            (glow?.Parent as Panel)?.Children.Remove(glow);
        }
    }
}