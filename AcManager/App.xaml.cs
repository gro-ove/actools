using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.AcSound;
using AcManager.Assets;
using AcManager.ContentRepair;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.DiscordRpc;
using AcManager.Internal;
using AcManager.Pages.ContentTools;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Pages.Miscellaneous;
using AcManager.Pages.Windows;
using AcManager.Pages.Workshop;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.GameProperties;
using AcManager.Tools.GameProperties.WeatherSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.DirectInput;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Helpers.PresetsPerMode;
using AcManager.Tools.Profile;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcManager.Tools.Starters;
using AcManager.Workshop;
using AcTools;
using AcTools.AcdEncryption;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.GenericMods;
using AcTools.Kn5File;
//#if !DEBUG
using AcTools.Kn5Tools;
//#endif
using AcTools.NeuralTyres;
using AcTools.Processes;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.WheelAngles;
using AcTools.Windows;
using AcTools.Windows.Input;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Win32;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using Newtonsoft.Json;
using StringBasedFilter;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace AcManager {
    public partial class App : IDisposable {
        private const string WebBrowserEmulationModeDisabledKey = "___webBrowserEmulationModeDisabled";

        public static void CreateAndRun(bool forceSoftwareRenderingMode) {
            FilesStorage.Initialize(EntryPoint.ApplicationDataDirectory);

            if (!AppArguments.GetBool(AppFlag.DisableLogging)) {
                var logFilename = EntryPoint.GetLogName("Main Log");
                Logging.Initialize(FilesStorage.Instance.GetFilename("Logs", logFilename), AppArguments.GetBool(AppFlag.OptimizeLogging));
                Logging.Write($"App version: {BuildInformation.AppVersion} ({BuildInformation.Platform}, {WindowsVersionHelper.GetVersion()})");
            }

            Storage.TemporaryBackupsDirectory = FilesStorage.Instance.GetTemporaryDirectory("Storages Backups");
            if (AppArguments.GetBool(AppFlag.DisableSaving)) {
                ValuesStorage.Initialize();
                CacheStorage.Initialize();
                AuthenticationStorage.Initialize();
            } else {
                ValuesStorage.Initialize(FilesStorage.Instance.GetFilename("Values.data"),
                        InternalUtils.GetValuesStorageEncryptionKey(),
                        AppArguments.GetBool(AppFlag.DisableValuesCompression));
                CacheStorage.Initialize(FilesStorage.Instance.GetFilename("Cache.data"), AppArguments.GetBool(AppFlag.DisableValuesCompression));
                AuthenticationStorage.Initialize(FilesStorage.Instance.GetFilename("Authentication.data"),
                        AppArguments.GetBool(AppFlag.DisableValuesCompression));
                if (MathUtils.Random(0, 10) == 0) {
                    LazierCached.Purge();
                }

                FatalErrorHandler.FatalError += OnFatalError;
            }

            if (AppArguments.GetBool(AppFlag.NoProxy, true)) {
                WebRequest.DefaultWebProxy = null;
            }

            NonfatalError.Initialize();
            LocaleHelper.InitializeAsync().Wait();

            var softwareRenderingModeWasEnabled = IsSoftwareRenderingModeEnabled();
            if (forceSoftwareRenderingMode) {
                ValuesStorage.Set(AppAppearanceManager.KeySoftwareRendering, true);
            }

            if (IsSoftwareRenderingModeEnabled()) {
                SwitchToSoftwareRendering();
            }

            var app = new App();

            // Some sort of safe mode
            if (forceSoftwareRenderingMode && !softwareRenderingModeWasEnabled) {
                Toast.Show("Safe mode", "Failed to start the last time, now CM uses software rendering", () => {
                    if (MessageDialog.Show(
                            "Would you like to switch back to hardware rendering? You can always do that in Settings/Appearance. App will be restarted.",
                            "Switch back", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        ValuesStorage.Set(AppAppearanceManager.KeySoftwareRendering, false);
                        Storage.SaveBeforeExit(); // Just in case
                        WindowsHelper.RestartCurrentApplication();
                    }
                });
            }

            var move = AppArguments.Get(AppFlag.MoveApp);
            if (move != null && File.Exists(move)) {
                for (var i = 0; i < 10; i++) {
                    if (FileUtils.TryToDelete(move) || !File.Exists(move)) break;
                    Thread.Sleep(100);
                }
                Toast.Show("App moved", $"App moved from AC root folder, now Oculus Rift should work better", () => {
                    var originalRemoved = File.Exists(move) ? "failed to remove original file" : "original file removed";
                    if (MessageDialog.Show(
                            $"New location is “{MainExecutingFile.Location}”, {originalRemoved}. Please don’t forget to recreate any shortcuts you might have created.",
                            "Content Manager is moved",
                            new MessageDialogButton {
                                [MessageBoxResult.Yes] = "View new location",
                                [MessageBoxResult.No] = UiStrings.Ok
                            }) == MessageBoxResult.Yes) {
                        WindowsHelper.ViewFile(MainExecutingFile.Location);
                    }
                });
            }

            app.Run();
        }

        private static void OnFatalError(object o, FatalErrorEventArgs args) {
            try {
                ValuesStorage.Remove("MainWindow_link");
                ValuesStorage.Remove("MainWindow__drive");
            } catch {
                // ignored
            }
        }

        public static bool IsSoftwareRenderingModeEnabled() {
            return AppArguments.GetBool(AppFlag.SoftwareRendering) || ValuesStorage.Get<bool>(AppAppearanceManager.KeySoftwareRendering)
                    || MainExecutingFile.Name.IndexOf(@"safe", StringComparison.OrdinalIgnoreCase) != -1;
        }

        private static void SwitchToSoftwareRendering() {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(30));
        }

        private AppHibernator _hibernator;

        private App() {
            if (AppArguments.GetBool(AppFlag.IgnoreHttps)) {
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            AppArguments.Set(AppFlag.SyncNavigation, ref ModernFrame.OptionUseSyncNavigation);
            AppArguments.Set(AppFlag.DisableTransitionAnimation, ref ModernFrame.OptionDisableTransitionAnimation);
            AppArguments.Set(AppFlag.RecentlyClosedQueueSize, ref LinkGroupFilterable.OptionRecentlyClosedQueueSize);

            AppArguments.Set(AppFlag.NoProxy, ref KunosApiProvider.OptionNoProxy);

            var proxy = AppArguments.Get(AppFlag.Proxy);
            if (!string.IsNullOrWhiteSpace(proxy)) {
                try {
                    var s = proxy.Split(':');
                    WebRequest.DefaultWebProxy = new WebProxy(s[0], FlexibleParser.ParseInt(s.ArrayElementAtOrDefault(1), 1080));
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            // TODO: AppArguments.Set(AppFlag.ScanPingTimeout, ref RecentManagerOld.OptionScanPingTimeout);
            AppArguments.Set(AppFlag.LanSocketTimeout, ref KunosApiProvider.OptionLanSocketTimeout);
            AppArguments.Set(AppFlag.LanPollTimeout, ref KunosApiProvider.OptionLanPollTimeout);
            AppArguments.Set(AppFlag.WebRequestTimeout, ref KunosApiProvider.OptionWebRequestTimeout);
            AppArguments.Set(AppFlag.DirectRequestTimeout, ref KunosApiProvider.OptionDirectRequestTimeout);
            AppArguments.Set(AppFlag.CommandTimeout, ref GameCommandExecutorBase.OptionCommandTimeout);
            AppArguments.Set(AppFlag.WeatherExtMode, ref WeatherProceduralHelper.Option24HourMode);

            AppArguments.Set(AppFlag.DisableAcRootChecking, ref AcPaths.OptionEaseAcRootCheck);
            AppArguments.Set(AppFlag.AcObjectsLoadingConcurrency, ref BaseAcManagerNew.OptionAcObjectsLoadingConcurrency);
            AppArguments.Set(AppFlag.SkinsLoadingConcurrency, ref CarObject.OptionSkinsLoadingConcurrency);
            AppArguments.Set(AppFlag.KunosCareerIgnoreSkippedEvents, ref KunosCareerEventsManager.OptionIgnoreSkippedEvents);
            AppArguments.Set(AppFlag.IgnoreMissingSkinsInKunosEvents, ref KunosEventObjectBase.OptionIgnoreMissingSkins);

            AppArguments.Set(AppFlag.CanPack, ref AcCommonObject.OptionCanBePackedFilter);
            AppArguments.Set(AppFlag.CanPackCars, ref CarObject.OptionCanBePackedFilter);

            AppArguments.Set(AppFlag.ForceToastFallbackMode, ref Toast.OptionFallbackMode);

            AppArguments.Set(AppFlag.SmartPresetsChangedHandling, ref UserPresetsControl.OptionSmartChangedHandling);
            AppArguments.Set(AppFlag.EnableRaceIniRestoration, ref Game.OptionEnableRaceIniRestoration);
            AppArguments.Set(AppFlag.EnableRaceIniTestMode, ref Game.OptionRaceIniTestMode);
            AppArguments.Set(AppFlag.RaceOutDebug, ref Game.OptionDebugMode);

            AppArguments.Set(AppFlag.NfsPorscheTribute, ref RaceGridViewModel.OptionNfsPorscheNames);
            AppArguments.Set(AppFlag.KeepIniComments, ref IniFile.OptionKeepComments);
            AppArguments.Set(AppFlag.AutoConnectPeriod, ref OnlineServer.OptionAutoConnectPeriod);
            AppArguments.Set(AppFlag.GenericModsLogging, ref GenericModsEnabler.OptionLoggingEnabled);
            AppArguments.Set(AppFlag.SidekickOptimalRangeThreshold, ref SidekickHelper.OptionRangeThreshold);
            AppArguments.Set(AppFlag.GoogleDriveLoaderDebugMode, ref GoogleDriveLoader.OptionDebugMode);
            AppArguments.Set(AppFlag.GoogleDriveLoaderManualRedirect, ref GoogleDriveLoader.OptionManualRedirect);
            AppArguments.Set(AppFlag.DebugPing, ref ServerEntry.OptionDebugPing);
            AppArguments.Set(AppFlag.DebugContentId, ref AcObjectNew.OptionDebugLoading);
            AppArguments.Set(AppFlag.JpegQuality, ref ImageUtilsOptions.JpegQuality);
            AppArguments.Set(AppFlag.FbxMultiMaterial, ref Kn5.OptionJoinToMultiMaterial);

            Acd.Factory = new AcdFactory();
//#if !DEBUG
            Kn5.Factory = Kn5New.GetFactoryInstance();
//#endif
            Lazier.SyncAction = ActionExtension.InvokeInMainThreadAsync;
            KeyboardListenerFactory.Register<KeyboardListener>();

            LimitedSpace.Initialize();
            DataProvider.Initialize();
            SteamIdHelper.Initialize(AppArguments.Get(AppFlag.ForceSteamId));
            TestKey();

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            if (!AppArguments.GetBool(AppFlag.PreventDisableWebBrowserEmulationMode) && (
                    ValuesStorage.Get<int>(WebBrowserEmulationModeDisabledKey) < WebBrowserHelper.EmulationModeDisablingVersion ||
                            AppArguments.GetBool(AppFlag.ForceDisableWebBrowserEmulationMode))) {
                try {
                    WebBrowserHelper.DisableBrowserEmulationMode();
                    ValuesStorage.Set(WebBrowserEmulationModeDisabledKey, WebBrowserHelper.EmulationModeDisablingVersion);
                } catch (Exception e) {
                    Logging.Warning("Can’t disable emulation mode: " + e);
                }
            }

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                Culture = CultureInfo.InvariantCulture
            };

            AcToolsLogging.Logger = (s, m, p, l) => Logging.Write($"{s} (AcTools)", m, p, l);
            AcToolsLogging.NonFatalErrorHandler = (s, c, e, b) => {
                if (b) {
                    NonfatalError.NotifyBackground(s, c, e);
                } else {
                    NonfatalError.Notify(s, c, e);
                }
            };

            AppArguments.Set(AppFlag.ControlsDebugMode, ref ControlsSettings.OptionDebugControlles);
            AppArguments.Set(AppFlag.ControlsRescanPeriod, ref DirectInputScanner.OptionMinRescanPeriod);
            var ignoreControls = AppArguments.Get(AppFlag.IgnoreControls);
            if (!string.IsNullOrWhiteSpace(ignoreControls)) {
                ControlsSettings.OptionIgnoreControlsFilter = Filter.Create(new StringTester(), ignoreControls);
            }

            var sseStart = AppArguments.Get(AppFlag.SseName);
            if (!string.IsNullOrWhiteSpace(sseStart)) {
                SseStarter.OptionStartName = sseStart;
            }
            AppArguments.Set(AppFlag.SseLogging, ref SseStarter.OptionLogging);

            FancyBackgroundManager.Initialize();
            if (AppArguments.Has(AppFlag.UiScale)) {
                AppearanceManager.Instance.AppScale = AppArguments.GetDouble(AppFlag.UiScale, 1d);
            }
            if (AppArguments.Has(AppFlag.WindowsLocationManagement)) {
                AppearanceManager.Instance.ManageWindowsLocation = AppArguments.GetBool(AppFlag.WindowsLocationManagement, true);
            }

            if (!InternalUtils.IsAllRight) {
                AppAppearanceManager.OptionCustomThemes = false;
            } else {
                AppArguments.Set(AppFlag.CustomThemes, ref AppAppearanceManager.OptionCustomThemes);
            }

            AppArguments.Set(AppFlag.FancyHintsDebugMode, ref FancyHint.OptionDebugMode);
            AppArguments.Set(AppFlag.FancyHintsMinimumDelay, ref FancyHint.OptionMinimumDelay);
            AppArguments.Set(AppFlag.WindowsVerbose, ref DpiAwareWindow.OptionVerboseMode);
            AppArguments.Set(AppFlag.ShowroomUiVerbose, ref LiteShowroomFormWrapperWithTools.OptionAttachedToolsVerboseMode);
            AppArguments.Set(AppFlag.BenchmarkReplays, ref GameDialog.OptionBenchmarkReplays);
            AppArguments.Set(AppFlag.HideRaceCancelButton, ref GameDialog.OptionHideCancelButton);
            AppArguments.Set(AppFlag.PatchSupport, ref PatchHelper.OptionPatchSupport);
            AppArguments.Set(AppFlag.CspReportsLocation, ref CspReportUtils.OptionLocation);
            AppArguments.Set(AppFlag.CmWorkshop, ref WorkshopClient.OptionUserAvailable);
            AppArguments.Set(AppFlag.CmWorkshopCreator, ref WorkshopClient.OptionCreatorAvailable);

            // Shared memory, now as an app flag
            SettingsHolder.Drive.WatchForSharedMemory = !AppArguments.GetBool(AppFlag.DisableSharedMemory);

            /*AppAppearanceManager.OptionIdealFormattingModeDefaultValue = AppArguments.GetBool(AppFlag.IdealFormattingMode,
                    !Equals(DpiAwareWindow.OptionScale, 1d));*/
            NonfatalErrorSolution.IconsDictionary = new Uri("/AcManager.Controls;component/Assets/IconData.xaml", UriKind.Relative);
            AppearanceManager.DefaultValuesSource = new Uri("/AcManager.Controls;component/Assets/ModernUI.Default.xaml", UriKind.Relative);
            AppAppearanceManager.Initialize(Pages.Windows.MainWindow.GetTitleLinksEntries());
            VisualExtension.RegisterInput<WebBlock>();

            ContentUtils.Register("AppStrings", AppStrings.ResourceManager);
            ContentUtils.Register("ControlsStrings", ControlsStrings.ResourceManager);
            ContentUtils.Register("ToolsStrings", ToolsStrings.ResourceManager);
            ContentUtils.Register("UiStrings", UiStrings.ResourceManager);
            LocalizationHelper.Use12HrFormat = SettingsHolder.Common.Use12HrTimeFormat;

            AcObjectsUriManager.Register(new UriProvider());

            {
                var uiFactory = new GameWrapperUiFactory();
                GameWrapper.RegisterFactory(uiFactory);
                ServerEntry.RegisterFactory(uiFactory);
            }

            GameWrapper.RegisterFactory(new DefaultAssistsFactory());
            LapTimesManager.Instance.SetListener();
            RaceResultsStorage.Instance.SetListener();

            AcError.RegisterFixer(new AcErrorFixer());
            AcError.RegisterSolutionsFactory(new SolutionsFactory());

            InitializePresets();

            SharingHelper.Initialize();
            SharingUiHelper.Initialize(AppArguments.GetBool(AppFlag.ModernSharing) ? new Win10SharingUiHelper() : null);

            {
                var addonsDir = FilesStorage.Instance.GetFilename("Addons");
                var pluginsDir = FilesStorage.Instance.GetFilename("Plugins");
                if (Directory.Exists(addonsDir) && !Directory.Exists(pluginsDir)) {
                    Directory.Move(addonsDir, pluginsDir);
                } else {
                    pluginsDir = FilesStorage.Instance.GetDirectory("Plugins");
                }

                PluginsManager.Initialize(pluginsDir);
                PluginsWrappers.Initialize(
                        new AssemblyResolvingWrapper(KnownPlugins.Fmod, FmodResolverService.Resolver),
                        new AssemblyResolvingWrapper(KnownPlugins.Fann, FannResolverService.Resolver),
                        new AssemblyResolvingWrapper(KnownPlugins.Magick, ImageUtils.MagickResolver),
                        new AssemblyResolvingWrapper(KnownPlugins.CefSharp, CefSharpResolverService.Resolver));
            }

            {
                var onlineMainListFile = FilesStorage.Instance.GetFilename("Online Servers", "Main List.txt");
                var onlineFavouritesFile = FilesStorage.Instance.GetFilename("Online Servers", "Favourites.txt");
                if (File.Exists(onlineMainListFile) && !File.Exists(onlineFavouritesFile)) {
                    Directory.Move(onlineMainListFile, onlineFavouritesFile);
                }
            }

            CupClient.Initialize();
            CupViewModel.Initialize();
            Superintendent.Initialize();
            ModsWebBrowser.Initialize();

            AppArguments.Set(AppFlag.OfflineMode, ref AppKeyDialog.OptionOfflineMode);

            WebBlock.DefaultDownloadListener = new WebDownloadListener();
            FlexibleLoader.CmRequestHandler = new CmRequestHandler();
            ContextMenus.ContextMenusProvider = new ContextMenusProvider();
            PrepareUi();

            AppShortcut.Initialize("Content Manager", "Content Manager");

            // If shortcut exists, make sure it has a proper app ID set for notifications
            if (File.Exists(AppShortcut.ShortcutLocation)) {
                AppShortcut.CreateShortcut();
            }

            AppIconService.Initialize(new AppIconProvider());

            Toast.SetDefaultAction(() => (Current.Windows.OfType<ModernWindow>().FirstOrDefault(x => x.IsActive) ??
                    Current.MainWindow as ModernWindow)?.BringToFront());
            BbCodeBlock.ImageClicked += OnBbImageClick;
            BbCodeBlock.OptionEmojiProvider = new EmojiProvider();
            BbCodeBlock.OptionImageCacheDirectory = FilesStorage.Instance.GetTemporaryFilename("Images");
            BbCodeBlock.OptionEmojiCacheDirectory = FilesStorage.Instance.GetTemporaryFilename("Emoji");

            BbCodeBlock.AddLinkCommand(new Uri("cmd://csp/enable"), new DelegateCommand(() => {
                using (var model = PatchSettingsModel.Create()) {
                    var item = model.Configs?
                            .FirstOrDefault(x => x.FileNameWithoutExtension == "general")?.Sections.GetByIdOrDefault("BASIC")?
                            .GetByIdOrDefault("ENABLED");
                    if (item != null) {
                        item.Value = @"1";
                    }
                }
            }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://csp/disable"), new DelegateCommand(() => {
                using (var model = PatchSettingsModel.Create()) {
                    var item = model.Configs?
                            .FirstOrDefault(x => x.FileNameWithoutExtension == "general")?.Sections.GetByIdOrDefault("BASIC")?
                            .GetByIdOrDefault("ENABLED");
                    if (item != null) {
                        item.Value = @"0";
                    }
                }
            }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://csp/update"), new DelegateCommand<string>(id => {
                var version = id.As(0);
                if (version == 0) {
                    Logging.Error($"Wrong parameter: {id}");
                    return;
                }

                var versionInfo = PatchUpdater.Instance.Versions.FirstOrDefault(x => x.Build == version);
                if (versionInfo == null) {
                    Logging.Error($"Version {version} is missing");
                    return;
                }

                PatchUpdater.Instance.InstallAsync(versionInfo, CancellationToken.None);
            }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://findMissing/car"), new DelegateCommand<string>(
                    id => { WindowsHelper.ViewInBrowser(SettingsHolder.Content.MissingContentSearch.GetUri(id, SettingsHolder.MissingContentType.Car)); }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://findMissing/track"), new DelegateCommand<string>(
                    id => { WindowsHelper.ViewInBrowser(SettingsHolder.Content.MissingContentSearch.GetUri(id, SettingsHolder.MissingContentType.Track)); }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://downloadMissing/car"), new DelegateCommand<string>(id => {
                var s = id.Split('|');
                IndexDirectDownloader.DownloadCarAsync(s[0], s.ArrayElementAtOrDefault(1)).Ignore();
            }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://downloadMissing/track"), new DelegateCommand<string>(id => {
                var s = id.Split('|');
                IndexDirectDownloader.DownloadTrackAsync(s[0], s.ArrayElementAtOrDefault(1)).Ignore();
            }));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://createNeutralLut"), new DelegateCommand<string>(id =>
                    NeutralColorGradingLut.CreateNeutralLut(id.As(16))));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://openPage/importantTips"), new DelegateCommand<string>(id =>
                    LinkCommands.NavigateLinkMainWindow.Execute(new Uri($"/Pages/About/ImportantTipsPage.xaml?Key={id}", UriKind.Relative))));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://openCarLodGeneratorDefinitions"), new DelegateCommand<string>(id =>
                    WindowsHelper.ViewFile(FilesStorage.Instance.GetContentFile(ContentCategory.CarLodsGeneration, "CommonDefinitions.json").Filename)));

            BbCodeBlock.AddLinkCommand(new Uri("cmd://findSrsServers"), new DelegateCommand(() => new ModernDialog {
                ShowTitle = false,
                ShowTopBlob = false,
                Title = "Connect to SimRacingSystem",
                Content = new ModernFrame {
                    Source = new Uri("/Pages/Drive/Online.xaml?Filter=SimRacingSystem", UriKind.Relative)
                },
                MinHeight = 400,
                MinWidth = 800,
                MaxHeight = DpiAwareWindow.UnlimitedSize,
                MaxWidth = DpiAwareWindow.UnlimitedSize,
                Padding = new Thickness(0),
                ButtonsMargin = new Thickness(8, -32, 8, 8),
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @".SrsJoinDialog"
            }.ShowDialog()));

            BbCodeBlock.DefaultLinkNavigator.PreviewNavigate += (sender, args) => {
                if (ArgumentsHandler.IsCmCommand(args.Uri)) {
                    ArgumentsHandler.ProcessArguments(new[] { args.Uri.ToString() }, true).Ignore();
                    args.Cancel = true;
                }
            };

            WorkshopLinkCommands.Initialize();

            AppArguments.SetSize(AppFlag.ImagesCacheLimit, ref BetterImage.OptionCacheTotalSize);
            AppArguments.SetSize(AppFlag.CarLodGeneratorCacheSize, ref CarGenerateLodsDialog.OptionCacheSize);
            AppArguments.Set(AppFlag.ImagesMarkCached, ref BetterImage.OptionMarkCached);
            BetterImage.RemoteUserAgent = CmApiProvider.UserAgent;
            BetterImage.RemoteCacheDirectory = BbCodeBlock.OptionImageCacheDirectory;
            GameWrapper.Started += (sender, args) => {
                BetterImage.CleanUpCache();
                GCHelper.CleanUp();
            };

            AppArguments.Set(AppFlag.UseVlcForAnimatedBackground, ref DynamicBackground.OptionUseVlc);
            Filter.OptionSimpleMatching = true;

            GameResultExtension.RegisterNameProvider(new GameSessionNameProvider());
            CarBlock.CustomShowroomWrapper = new CustomShowroomWrapper();
            CarBlock.CarSetupsView = new CarSetupsView();
            SettingsHolder.Content.OldLayout = AppArguments.GetBool(AppFlag.CarsOldLayout);

            var acRootIsFine = Superintendent.Instance.IsReady && !AcRootDirectorySelector.IsReviewNeeded();
            if (acRootIsFine && SteamStarter.Initialize(AcRootDirectory.Instance.Value, false)) {
                if (SettingsHolder.Drive.SelectedStarterType != SettingsHolder.DriveSettings.SteamStarterType) {
                    SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.SteamStarterType;
                    Toast.Show("Starter changed to replacement", "Enjoy Steam being included into CM");
                }
            } else if (SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.SteamStarterType) {
                SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.DefaultStarterType;
                Toast.Show($"Starter changed to {SettingsHolder.Drive.SelectedStarterType.DisplayName}", "Steam Starter is unavailable", () => {
                    ModernDialog.ShowMessage(
                            "To use Steam Starter, please make sure CM is taken place of the official launcher and AC root directory is valid.",
                            "Steam Starter is unavailable", MessageBoxButton.OK);
                });
            }

            InitializeUpdatableStuff();
            BackgroundInitialization();

            FatalErrorMessage.Register(new AppRestartHelper());
            ImageUtils.SafeMagickWrapper = fn => {
                try {
                    return fn();
                } catch (OutOfMemoryException e) {
                    NonfatalError.Notify(ToolsStrings.MagickNet_CannotLoad, ToolsStrings.MagickNet_CannotLoad_Commentary, e);
                } catch (Exception e) {
                    NonfatalError.Notify(ToolsStrings.MagickNet_CannotLoad, e);
                }
                return null;
            };

            DataFileBase.ErrorsCatcher = new DataSyntaxErrorCatcher();
            AppArguments.Set(AppFlag.SharedMemoryLiveReadingInterval, ref AcSharedMemory.OptionLiveReadingInterval);
            AcSharedMemory.Initialize();

            AppArguments.Set(AppFlag.RunRaceInformationWebserver, ref PlayerStatsManager.OptionRunStatsWebserver);
            AppArguments.Set(AppFlag.RaceInformationWebserverFile, ref PlayerStatsManager.OptionWebserverFilename);

            PlayerStatsManager.Instance.SetListener();
            RhmService.Instance.SetListener();

            WheelOptionsBase.SetStorage(new WheelAnglesStorage());

            _hibernator = new AppHibernator();
            _hibernator.SetListener();

            VisualCppTool.Initialize(FilesStorage.Instance.GetDirectory("Plugins", "NativeLibs"));

            try {
                SetRenderersOptions();
            } catch (Exception e) {
                VisualCppTool.OnException(e, null);
            }

            CommonFixes.Initialize();

            CmPreviewsTools.MissingShowroomHelper = new CarUpdatePreviewsDialog.MissingShowroomHelper();

            // Paint shop+livery generator?
            LiteShowroomTools.LiveryGenerator = new LiveryGenerator();

            // Discord
            if (AppArguments.Has(AppFlag.DiscordCmd)) {
                // Do not show main window and wait for futher instructions?
            }

            if (SettingsHolder.Integrated.DiscordIntegration) {
                AppArguments.Set(AppFlag.DiscordVerbose, ref DiscordConnector.OptionVerboseMode);
                DiscordConnector.Initialize(AppArguments.Get(AppFlag.DiscordClientId) ?? InternalUtils.GetDiscordClientId(), new DiscordHandler());
                DiscordImage.OptionDefaultImage = @"track_ks_brands_hatch";
                GameWrapper.Started += (sender, args) => args.StartProperties.SetAdditional(new GameDiscordPresence(args.StartProperties, args.Mode));
            }

            // Reshade?
            var loadReShade = AppArguments.GetBool(AppFlag.ForceReshade);
            if (!loadReShade && string.Equals(AppArguments.Get(AppFlag.ForceReshade), @"kn5only", StringComparison.OrdinalIgnoreCase)) {
                loadReShade = AppArguments.Values.Any(x => x.EndsWith(@".kn5", StringComparison.OrdinalIgnoreCase));
            }

            if (loadReShade) {
                var reshade = Path.Combine(MainExecutingFile.Directory, "dxgi.dll");
                if (File.Exists(reshade)) {
                    Kernel32.LoadLibrary(reshade);
                }
            }

            // Auto-show that thing
            InstallAdditionalContentDialog.Initialize();

            // Make sure Steam is running
            if (SettingsHolder.Common.LaunchSteamAtStart) {
                LaunchSteam().Ignore();
            }

            // Check and apply FTH fix if necessary
            CheckFaultTolerantHeap().Ignore();
            RaceUTemporarySkinsHelper.Initialize();

            // Initializing CSP handler
            if (PatchHelper.OptionPatchSupport) {
                PatchUpdater.Initialize();
            }

            // Let’s roll
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            new AppUi(this).Run(() => {
                if (PatchHelper.OptionPatchSupport) {
                    PatchUpdater.Instance.Updated += OnPatchUpdated;
                }
            });
        }

        private static async Task CheckFaultTolerantHeap() {
            try {
                await Task.Delay(500);
                if (ValuesStorage.Get(".fth.shown2", false) && FaultTolerantHeapFix.Check()) {
                    NonfatalError.NotifyBackground("Performance issue detected",
                            "Assetto Corsa performance is negatively affected by FTH. Content Manager can try to fix it.",
                            solutions: new[] {
                                new NonfatalErrorSolution("Try to fix the issue", cancellation => {
                                    FaultTolerantHeapFix.CheckAndFixAsync().Ignore();
                                    return Task.Delay(0);
                                })
                            });
                } else {
                    FaultTolerantHeapFix.CheckAndFixAsync().Ignore();
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private static async Task LaunchSteam() {
            await Task.Delay(500);
            await Task.Run(() => {
                try {
                    SteamRunningHelper.EnsureSteamIsRunning(true, false);
                } catch (Exception e) {
                    Logging.Error(e);
                }
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetRenderersOptions() {
            AppArguments.Set(AppFlag.TrackMapGeneratorMaxSize, ref TrackMapRenderer.OptionMaxSize);
        }

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Switch.Level = BindingErrorTraceListener.GetSourceLevels();
            PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
        }

        private class CarSetupsView : ICarSetupsView {
            public void Open(CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None, bool forceNewWindow = false) {
                CarSetupsListPage.Open(car, forceRemoteSource, forceNewWindow);
            }
        }

        private class LiveryGenerator : ILiveryGenerator {
            public Task CreateLiveryAsync(CarSkinObject skin, Color[] colors, string preferredStyle) {
                return LiveryIconEditor.GenerateAsync(skin, colors, preferredStyle);
            }
        }

        private class DataSyntaxErrorCatcher : ISyntaxErrorsCatcher {
            public void Catch(DataFileBase file, int line) {
                if (file.Filename != null) {
                    NonfatalError.NotifyBackground(string.Format(ToolsStrings.SyntaxError_Unpacked, Path.GetFileName(file.Filename), line),
                            ToolsStrings.SyntaxError_Commentary, null, new[] {
                                new NonfatalErrorSolution(ToolsStrings.SyntaxError_Solution, token => {
                                    WindowsHelper.OpenFile(file.Filename);
                                    return Task.Delay(0, token);
                                })
                            });
                } else {
                    NonfatalError.NotifyBackground(string.Format(ToolsStrings.SyntaxError_Packed,
                            $"{file.Name} ({Path.GetFileName((file.Data as IDataWrapper)?.Location ?? @"?")})", line), ToolsStrings.SyntaxError_Commentary);
                }
            }
        }

        private static void PrepareUi() {
            try {
                ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(true));
                ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(300));
                ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(600));
                ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(60000));
                ItemsControl.IsTextSearchCaseSensitiveProperty.OverrideMetadata(typeof(ComboBox), new FrameworkPropertyMetadata(true));

                if (AppAppearanceManager.Instance.DisallowTransparency) {
                    DisableTransparencyHelper.Disable();
                }
            } catch (Exception e) {
                Logging.Error(e);
            }

            PopupHelper.Initialize();
            CommonCommands.SetHelper(new SomeCommonCommandsHelper());
        }

        private async void TestKey() {
            InternalUtils.Initialize(FilesStorage.Instance.GetFilename("License.txt"), SteamIdHelper.Instance.Value);
            if (InternalUtils.Revoked == null) return;

            await Task.Delay(3000);

            ValuesStorage.SetEncrypted(AppKeyDialog.AppKeyRevokedKey, InternalUtils.Revoked);
            InternalUtils.SetKey(null, null);

            Current.Dispatcher?.Invoke(() => {
                if (Current?.MainWindow is MainWindow && Current.MainWindow.IsActive) {
                    AppKeyDialog.ShowRevokedMessage();
                }
            });
        }

        private static void RevertFileChanges() {
            PresetsPerModeBackup.Revert();
            WeatherSpecificCloudsHelper.Revert();
            WeatherSpecificTyreSmokeHelper.Revert();
            WeatherSpecificVideoSettingsHelper.Revert();
            WeatherSpecificLightingHelper.Revert();
            CarSpecificControlsPresetHelper.Revert();
            CarSpecificFanatecSettingsHelper.Revert();
            CarCustomDataHelper.Revert();
            CarExtendedPhysicsHelper.Revert();
            CopyFilterToSystemForOculusHelper.Revert();
            AcShadowsPatcher.Revert();
        }

        private static async void BackgroundInitialization() {
            try {
#if DEBUG
                CupClient.Instance?.LoadRegistries().Ignore();
#endif

                if (AcRootDirectory.Instance.Value != null && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.CmLaunchCommand)) {
                    GameCommandExecutorBase.Execute(SettingsHolder.Drive.CmLaunchCommand, AcRootDirectory.Instance.Value);
                }

                await Task.Delay(500);
                AppArguments.Set(AppFlag.SimilarThreshold, ref CarAnalyzer.OptionSimilarThreshold);

                if (SettingsHolder.Drive.ScanControllersAutomatically) {
                    try {
                        InitializeDirectInputScanner();
                    } catch (Exception e) {
                        VisualCppTool.OnException(e, null);
                    }
                }

                string additional = null;
                AppArguments.Set(AppFlag.SimilarAdditionalSourceIds, ref additional);
                if (!string.IsNullOrWhiteSpace(additional)) {
                    CarAnalyzer.OptionSimilarAdditionalSourceIds = additional.Split(';', ',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                }

                await Task.Delay(500);
                if (AppArguments.Has(AppFlag.TestIfAcdAvailable) && !Acd.IsAvailable) {
                    NonfatalError.NotifyBackground(@"This build can’t work with encrypted ACD-files");
                }

                if (AppUpdater.JustUpdated && SettingsHolder.Common.ShowDetailedChangelog) {
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

                    Logging.Write("Changelog entries: " + changelog.Count);
                    if (changelog.Any()) {
                        Toast.Show(AppStrings.App_AppUpdated, AppStrings.App_AppUpdated_Details, () => {
                            ModernDialog.ShowMessage(changelog.Select(x => $@"[b]{x.Version}[/b]{Environment.NewLine}{x.Changes}")
                                    .JoinToString(Environment.NewLine.RepeatString(2)), AppStrings.Changelog_RecentChanges_Title,
                                    MessageBoxButton.OK);
                        });
                    }
                }

                await Task.Delay(1500);
                RevertFileChanges();

                await Task.Delay(1500);
                CustomUriSchemeHelper.Initialize();

#if !DEBUG
                CupClient.Instance?.LoadRegistries().Ignore();
#endif

                await Task.Delay(1500);
                ExtraProgressRings.Initialize();

                await Task.Delay(3500);
                await Task.Run(() => {
                    foreach (var f in from file in Directory.GetFiles(FilesStorage.Instance.GetDirectory("Logs"))
                        where file.EndsWith(@".txt") || file.EndsWith(@".log") || file.EndsWith(@".json")
                        let info = new FileInfo(file)
                        where info.LastWriteTime < DateTime.Now - TimeSpan.FromDays(3)
                        select info) {
                        f.Delete();
                    }
                });

                await Task.Delay(5000);
                await Task.Run(() => {
                    foreach (var f in new DirectoryInfo(FilesStorage.Instance.GetTemporaryDirectory()).GetFiles("*", SearchOption.AllDirectories)
                            .Where(x => x.LastAccessTime < DateTime.Now - TimeSpan.FromDays(30) && x.LastWriteTime < DateTime.Now - TimeSpan.FromDays(30))) {
                        if (f.Name == "Startup.Profile") continue;
                        Logging.Debug($"Delete old temporary file: {f.FullName}");
                        f.Delete();
                    }
                });
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void OnBbImageClick(object sender, BbCodeImageEventArgs e) {
            var index = e.BlockImages?.Select(x => x.Url).IndexOf(e.ImageUrl);
            (index > -1 ?
                    new ImageViewer<BbCodeImageInformation>(e.BlockImages, index.Value,
                            x => Task.FromResult((object)x?.Url),
                            x => x?.Description) {
                                HorizontalDetailsAlignment = HorizontalAlignment.Center
                            } :
                    new ImageViewer(e.ImageUrl)).ShowDialog();
        }

        private void InitializeUpdatableStuff() {
            DataUpdater.Initialize();
            DataUpdater.Instance.Updated += OnDataUpdated;

            if (AcRootDirectory.Instance.IsFirstRun) {
                AppUpdater.OnFirstRun();
            }

            AppUpdater.Initialize();
            AppUpdater.Instance.Updated += OnAppUpdated;

            if (LocaleHelper.JustUpdated) {
                Toast.Show(AppStrings.App_LocaleUpdated, string.Format(AppStrings.App_DataUpdated_Details, LocaleHelper.LoadedVersion));
            }

            LocaleUpdater.Initialize(LocaleHelper.LoadedVersion);
            LocaleUpdater.Instance.Updated += OnLocaleUpdated;
        }

        private void OnDataUpdated(object sender, EventArgs e) {
            Toast.Show(AppStrings.App_DataUpdated, string.Format(AppStrings.App_DataUpdated_Details, DataUpdater.Instance.InstalledVersion));
        }

        private void OnPatchUpdated(object sender, EventArgs e) {
            /*if (PatchUpdater.Instance.ShowDetailedChangelog.Value) {
                Toast.Show("Shaders Patch updated", string.Format(AppStrings.App_DataUpdated_Details, PatchUpdater.Instance.DisplayInstalledVersion));
            }*/
        }

        private void OnAppUpdated(object sender, EventArgs e) {
            Toast.Show(AppStrings.App_NewVersion,
                    string.Format(AppStrings.App_NewVersion_Details, AppUpdater.Instance.ReadyToUpdateVersion),
                    () => AppUpdater.Instance.FinishUpdateCommand.Execute(null));
        }

        private void OnLocaleUpdated(object sender, EventArgs e) {
            if (string.Equals(CultureInfo.CurrentUICulture.Name, SettingsHolder.Locale.LocaleName, StringComparison.OrdinalIgnoreCase)) {
                Toast.Show(AppStrings.App_LocaleUpdated, AppStrings.App_LocaleUpdated_Details, WindowsHelper.RestartCurrentApplication);
            }
        }

        private static void InitializePresets() {
            PresetsManager.Initialize(FilesStorage.Instance.GetDirectory("Presets"));
            DefaultPresets.Initialize();
            TrackStatesHelper.Initialize();
        }

        private void OnProcessExit(object sender, EventArgs args) {
            Logging.Flush();
            Storage.SaveBeforeExit();
            KunosCareerProgress.SaveBeforeExit();
            UserChampionshipsProgress.SaveBeforeExit();
            RhmService.Instance.Dispose();
            DiscordConnector.Instance?.Dispose();
            try {
                ShutdownDirectInputScanner();
            } catch (Exception e) {
                Logging.Error(e.Message);
            }
            Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeDirectInputScanner() {
            DirectInputScanner.GetAsync().Ignore();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShutdownDirectInputScanner() {
            DirectInputScanner.Shutdown();
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _hibernator);
        }
    }
}