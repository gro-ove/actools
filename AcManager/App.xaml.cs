using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Plugins;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager {
    public partial class App {
        private const string WebBrowserEmulationModeDisabledKey = "___webBrowserEmulationModeDisabled";

        public App() {
            if (AppArguments.GetBool(AppFlag.IgnoreSystemProxy, true)) {
                WebRequest.DefaultWebProxy = null;
            }

            FilesStorage.Initialize(EntryPoint.ApplicationDataDirectory);

            AppArguments.Set(AppFlag.SyncNavigation, ref ModernFrame.OptionUseSyncNavigation);
            AppArguments.Set(AppFlag.DisableTransitionAnimation, ref ModernFrame.OptionDisableTransitionAnimation);
            AppArguments.Set(AppFlag.RecentlyClosedQueueSize, ref LinkGroupFilterable.OptionRecentlyClosedQueueSize);

            AppArguments.Set(AppFlag.ForceSteamId, ref SteamIdHelper.OptionForceValue);
            
            AppArguments.Set(AppFlag.IgnoreSystemProxy, ref KunosApiProvider.OptionIgnoreSystemProxy);
            AppArguments.Set(AppFlag.ScanPingTimeout, ref RecentManager.OptionScanPingTimeout);
            AppArguments.Set(AppFlag.LanSocketTimeout, ref KunosApiProvider.OptionLanSocketTimeout);
            AppArguments.Set(AppFlag.LanPollTimeout, ref KunosApiProvider.OptionLanPollTimeout);
            AppArguments.Set(AppFlag.WebRequestTimeout, ref KunosApiProvider.OptionWebRequestTimeout);
            AppArguments.Set(AppFlag.CommandTimeout, ref GameCommandExecutorBase.OptionCommandTimeout);

            AppArguments.Set(AppFlag.DisableAcRootChecking, ref AcRootDirectory.OptionDisableChecking);
            AppArguments.Set(AppFlag.AcObjectsLoadingConcurrency, ref BaseAcManagerNew.OptionAcObjectsLoadingConcurrency);
            AppArguments.Set(AppFlag.SkinsLoadingConcurrency, ref CarObject.OptionSkinsLoadingConcurrency);
            AppArguments.Set(AppFlag.KunosCareerIgnoreSkippedEvents, ref KunosCareerEventsManager.OptionIgnoreSkippedEvents);

            AppArguments.Set(AppFlag.ForceToastFallbackMode, ref Toast.OptionFallbackMode);

            AppArguments.Set(AppFlag.SmartPresetsChangedHandling, ref UserPresetsControl.OptionSmartChangedHandling);
            AppArguments.Set(AppFlag.EnableRaceIniRestoration, ref Game.OptionEnableRaceIniRestoration);
            AppArguments.Set(AppFlag.EnableRaceIniTestMode, ref Game.OptionRaceIniTestMode);

            AppArguments.Set(AppFlag.LiteStartupModeSupported, ref Pages.Windows.MainWindow.OptionLiteModeSupported);

            NonfatalError.Register(new NonfatalErrorNotifier());

            if (!AppArguments.GetBool(AppFlag.DisableLogging)) {
                var logFilename = FilesStorage.Instance.GetFilename("Logs", "Main Log.txt");
                if (File.Exists(logFilename)) {
                    File.Move(logFilename, $"{logFilename.ApartFromLast(@".txt", StringComparison.OrdinalIgnoreCase)}_{DateTime.Now.ToUnixTimestamp()}.txt");
                    DeleteOldLogs();
                }

                Logging.Initialize(FilesStorage.Instance.GetFilename("Logs", logFilename));
                Logging.Write("App version: " + BuildInformation.AppVersion);
            }

            if (!AppArguments.GetBool(AppFlag.DisableSaving)) {
                ValuesStorage.Initialize(FilesStorage.Instance.GetFilename("Values.data"), AppArguments.GetBool(AppFlag.DisableValuesCompression));
            }

            LimitedSpace.Initialize();
            LimitedStorage.Initialize();

            DataProvider.Initialize();

            TestKey();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            if (!AppArguments.GetBool(AppFlag.PreventDisableWebBrowserEmulationMode) && (
                    ValuesStorage.GetInt(WebBrowserEmulationModeDisabledKey) < WebBrowserHelper.EmulationModeDisablingVersion ||
                            AppArguments.GetBool(AppFlag.ForceDisableWebBrowserEmulationMode))) {
                try {
                    WebBrowserHelper.DisableBrowserEmulationMode();
                    ValuesStorage.Set(WebBrowserEmulationModeDisabledKey, WebBrowserHelper.EmulationModeDisablingVersion);
                } catch (Exception e) {
                    Logging.Warning("cannot disable emulation mode: " + e);
                }
            }

            FancyBackgroundManager.Initialize();
            DpiAwareWindow.OptionScale = AppArguments.GetDouble(AppFlag.UiScale, 1d);
            AppAppearanceManager.OptionIdealFormattingModeDefaultValue = AppArguments.GetBool(AppFlag.IdealFormattingMode,
                    !Equals(DpiAwareWindow.OptionScale, 1d));
            AppAppearanceManager.Initialize();

            AcObjectsUriManager.Register(new UriProvider());
            GameWrapper.RegisterFactory(new GameWrapperUiFactory());
            AcError.RegisterFixer(new AcErrorFixer());
            AcError.RegisterSolutionsFactory(new SolutionsFactory());

            InitializeUpdatableStuff();
            InitializePresets();

            SharingHelper.Initialize();
            SharingUiHelper.Initialize();

            var addonsDir = FilesStorage.Instance.GetFilename("Addons");
            var pluginsDir = FilesStorage.Instance.GetFilename("Plugins");
            if (Directory.Exists(addonsDir) && !Directory.Exists(pluginsDir)) {
                Directory.Move(addonsDir, pluginsDir);
            } else {
                pluginsDir = FilesStorage.Instance.GetDirectory("Plugins");
            }

            PluginsManager.Initialize(pluginsDir);
            PluginsWrappers.Initialize(
                    new MagickPluginWrapper(),
                    new AwesomiumPluginWrapper(),
                    new StarterPlus());

            SteamIdHelper.Initialize();
            OnlineManager.Initialize();
            LanManager.Initialize();
            RecentManager.Initialize();
            Superintendent.Initialize();

            AppArguments.Set(AppFlag.OfflineMode, ref AppKeyDialog.OptionOfflineMode);

            PrepareUi();
            var iconUri = new Uri("pack://application:,,,/Content Manager;component/Assets/Icons/Icon.ico",
                    UriKind.Absolute);
            CustomShowroomWrapper.SetDefaultIcon(iconUri);
            Toast.SetDefaultIcon(iconUri);
            Toast.SetDefaultAction(() => (Current.MainWindow as ModernWindow)?.BringToFront());

            BbCodeBlock.ImageClicked += BbCodeBlock_ImageClicked;

            StartupUri = new Uri(Superintendent.Instance.IsReady ?
                    @"Pages/Windows/MainWindow.xaml" : @"Pages/Dialogs/AcRootDirectorySelector.xaml", UriKind.Relative);

            BackgroundInitialization();
        }

        private void PrepareUi() {
            try {
                ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(true));
                ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(700));
                ItemsControl.IsTextSearchCaseSensitiveProperty.OverrideMetadata(typeof(ComboBox), new FrameworkPropertyMetadata(true));
            } catch (Exception e) {
                Logging.Warning("Can’t prepare UI: " + e);
            }
        }

        private async void TestKey() {
            AppKeyHolder.Initialize(FilesStorage.Instance.GetFilename("License.txt"));
            var revoked = AppKeyHolder.IsAllRight ? null : AppKeyHolder.Instance.Revoked;

            await Task.Delay(500);
            
            if (revoked != null || AppKeyHolder.IsAllRight && await InternalUtils.CheckKeyAsync(AppKeyHolder.Key, CmApiProvider.UserAgent) == false) {
                ValuesStorage.SetEncrypted(AppKeyDialog.AppKeyRevokedKey, revoked ?? AppKeyHolder.Key);
                AppKeyHolder.Instance.SetKey(null);

                Dispatcher.Invoke(AppKeyDialog.ShowRevokedMessage);
                if (revoked == null) {
                    Dispatcher.Invoke(WindowsHelper.RestartCurrentApplication);
                }
            }
        }

        private async void BackgroundInitialization() {
            await Task.Delay(1500);
            CustomUriSchemeHelper.EnsureRegistered();
            WeatherSpecificCloudsHelper.RestoreIfReplaced();
        }

        private async void DeleteOldLogs() {
            await Task.Delay(5000);
            await Task.Run(() => {
                var directory = FilesStorage.Instance.GetDirectory("Logs");
                foreach (var f in from file in Directory.GetFiles(directory)
                                  where file.EndsWith(@".txt") || file.EndsWith(@".json")
                                  let info = new FileInfo(file)
                                  where info.CreationTime < DateTime.Now.AddDays(-10)
                                  select info){
                    f.Delete();
                }
            });
        }

        private void BbCodeBlock_ImageClicked(object sender, BbCodeImageEventArgs e) {
            new ImageViewer(e.ImageUri.ToString()).ShowDialog();
        }

        private void InitializeUpdatableStuff() {
            ContentSyncronizer.Initialize();
            ContentSyncronizer.Instance.PropertyChanged += ContentSyncronizer_PropertyChanged;

            AppUpdater.Initialize();
            AppUpdater.Instance.PropertyChanged += AppUpdater_PropertyChanged;
        }

        private void ContentSyncronizer_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ContentSyncronizer.InstalledVersion)) {
                Toast.Show(AcManager.AppStrings.App_ContentUpdated, string.Format(AcManager.AppStrings.App_ContentUpdated_Details, ContentSyncronizer.Instance.InstalledVersion));
            }
        }

        private void AppUpdater_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AppUpdater.UpdateIsReady)) {
                Toast.Show(AcManager.AppStrings.App_NewVersion,
                        string.Format(AcManager.AppStrings.App_NewVersion_Details, AppUpdater.Instance.UpdateIsReady), () => {
                            AppUpdater.Instance.FinishUpdateCommand.Execute(null);
                        });
            }
        }

        private static void InitializePresets() {
            PresetsManager.Initialize(FilesStorage.Instance.GetDirectory("Presets"));
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.PresetPreviewsKunos, @"Previews", @"Kunos");
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsGamer, @"Assists", Controls.ControlsStrings.AssistsPreset_Gamer);
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsIntermediate, @"Assists", Controls.ControlsStrings.AssistsPreset_Intermediate);
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsPro, @"Assists", Controls.ControlsStrings.AssistsPreset_Pro);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            ValuesStorage.SaveBeforeExit();
            KunosCareerProgress.SaveBeforeExit();
        }
    }
}
