using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Helpers;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Controls.Presentation;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Properties;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager {
    public partial class App {
        private const string WebBrowserEmulationModeDisabledKey = "___webBrowserEmulationModeDisabled";

        private static string GetLocalApplicationDataDirectory() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcTools Content Manager");
        }

        public App() {
            if (AppArguments.GetBool(AppFlag.IgnoreSystemProxy, true)) {
                WebRequest.DefaultWebProxy = null;
            }

            AppArguments.Initialize(Environment.GetCommandLineArgs().Skip(1));
            FilesStorage.Initialize(AppArguments.Get(AppFlag.StorageLocation) ?? GetLocalApplicationDataDirectory());
            AppArguments.AddFromFile(FilesStorage.Instance.GetFilename("Arguments.txt"));

            AppArguments.Set(AppFlag.SyncNavigation, ref ModernFrame.OptionUseSyncNavigation);
            AppArguments.Set(AppFlag.DisableTransitionAnimation, ref ModernFrame.OptionDisableTransitionAnimation);
            AppArguments.Set(AppFlag.RecentlyClosedQueueSize, ref LinkGroupFilterable.OptionRecentlyClosedQueueSize);

            AppArguments.Set(AppFlag.ForceSteamId, ref SteamIdHelper.OptionForceValue);

            AppArguments.Set(AppFlag.PingTimeout, ref KunosApiProvider.OptionPingTimeout);
            AppArguments.Set(AppFlag.IgnoreSystemProxy, ref KunosApiProvider.OptionIgnoreSystemProxy);
            AppArguments.Set(AppFlag.ScanPingTimeout, ref RecentManager.OptionScanPingTimeout);
            AppArguments.Set(AppFlag.LanSocketTimeout, ref KunosApiProvider.OptionLanSocketTimeout);
            AppArguments.Set(AppFlag.LanPollTimeout, ref KunosApiProvider.OptionLanPollTimeout);
            AppArguments.Set(AppFlag.WebRequestTimeout, ref KunosApiProvider.OptionWebRequestTimeout);
            AppArguments.Set(AppFlag.CommandTimeout, ref GameCommandExecutor.OptionCommandTimeout);

            AppArguments.Set(AppFlag.PingConcurrency, ref BaseOnlineManager.OptionConcurrentThreadsNumber);
            AppArguments.Set(AppFlag.AlwaysGetInformationDirectly, ref ServerEntry.OptionAlwaysGetInformationDirectly);

            AppArguments.Set(AppFlag.DisableAcRootChecking, ref AcRootDirectory.OptionDisableChecking);
            AppArguments.Set(AppFlag.AcObjectsLoadingConcurrency, ref BaseAcManagerNew.OptionAcObjectsLoadingConcurrency);
            AppArguments.Set(AppFlag.SkinsLoadingConcurrency, ref CarObject.OptionSkinsLoadingConcurrency);
            AppArguments.Set(AppFlag.KunosCareerIgnoreSkippedEvents, ref KunosCareerEventsManager.OptionIgnoreSkippedEvents);

            AppArguments.Set(AppFlag.ForceToastFallbackMode, ref Toast.OptionFallbackMode);

            AppArguments.Set(AppFlag.SmartPresetsChangedHandling, ref UserPresetsControl.OptionSmartChangedHandling);
            AppArguments.Set(AppFlag.EnableRaceIniRestoration, ref Game.OptionEnableRaceIniRestoration);

            AppArguments.Set(AppFlag.LiteStartupModeSupported, ref Pages.Windows.MainWindow.OptionLiteModeSupported);

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            NonfatalError.Register(new NonfatalErrorNotifier());

            if (!AppArguments.GetBool(AppFlag.DisableLogging)) {
                var logFilename = FilesStorage.Instance.GetFilename("Logs", "Main Log.txt");
                if (File.Exists(logFilename)) {
                    File.Move(logFilename, $"{logFilename.ApartFromLast(".txt", StringComparison.OrdinalIgnoreCase)}_{DateTime.Now.ToUnixTimestamp()}.txt");
                    DeleteOldLogs();
                }

                Logging.Initialize(FilesStorage.Instance.GetFilename("Logs", logFilename));
                Logging.Write("App version: " + BuildInformation.AppVersion);
            }
            ValuesStorage.Initialize(FilesStorage.Instance.GetFilename("Values.data"), AppArguments.GetBool(AppFlag.DisableValuesCompression));
            DataProvider.Initialize();

            TestKey();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            if (!AppArguments.GetBool(AppFlag.PreventDisableWebBrowserEmulationMode) && (
                    ValuesStorage.GetInt(WebBrowserEmulationModeDisabledKey) < 2 || AppArguments.GetBool(AppFlag.ForceDisableWebBrowserEmulationMode))) {
                try {
                    WebBrowserHelper.DisableBrowserEmulationMode();
                    ValuesStorage.Set(WebBrowserEmulationModeDisabledKey, 2);
                } catch (Exception e) {
                    Logging.Warning("cannot disable emulation mode: " + e);
                }
            }

            FancyBackgroundManager.Initialize();
            DpiAwareWindow.OptionScale = AppArguments.GetDouble(AppFlag.UiScale, 1d);
            AppAppearanceManager.OptionIdealFormattingModeDefaultValue = AppArguments.GetBool(AppFlag.IdealFormattingMode,
                    !Equals(DpiAwareWindow.OptionScale, 1d));
            Logging.Write("HERE: " + AppAppearanceManager.OptionIdealFormattingModeDefaultValue);
            AppAppearanceManager.Initialize();

            AcObjectsUriManager.Register(new UriProvider());
            SolversManager.RegisterFactory(new UiSolversFactory());
            GameWrapper.RegisterFactory(new GameWrapperUiFactory());
            AcError.Register(new UiAcErrorFixer());

            InitializeUpdatableStuff();
            InitializePresets();

            SharingHelper.Initialize();
            SharingUiHelper.Initialize();

            AppAddonsManager.Initialize(FilesStorage.Instance.GetDirectory("Addons"));
            StarterPlus.Initialize();
            InitializeMagickAddonAsync().Forget();

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
                    "Pages/Windows/MainWindow.xaml" : "Pages/Dialogs/AcRootDirectorySelector.xaml", UriKind.Relative);

            RegisterUriScheme();
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

        private async void RegisterUriScheme() {
            await Task.Delay(1500);
            CustomUriSchemeHelper.EnsureRegistered();
        }

        private async void DeleteOldLogs() {
            await Task.Delay(5000);
            await Task.Run(() => {
                var directory = FilesStorage.Instance.GetDirectory("Logs");
                foreach (var f in from file in Directory.GetFiles(directory)
                                  where file.EndsWith(".txt") || file.EndsWith(".json")
                                  let info = new FileInfo(file)
                                  where info.CreationTime < DateTime.Now.AddDays(-3)
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
            if (e.PropertyName == "InstalledVersion") {
                Toast.Show("Content Updated", $"Current version: {ContentSyncronizer.Instance.InstalledVersion}");
            }
        }

        private void AppUpdater_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "UpdateIsReady") {
                Toast.Show("New Version", $"Update is ready: {AppUpdater.Instance.UpdateIsReady}", () => {
                    AppUpdater.Instance.FinishUpdateCommand.Execute(null);
                });
            }
        }

        private static void InitializePresets() {
            PresetsManager.Initialize(FilesStorage.Instance.GetDirectory("Presets"));
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.PresetPreviewsKunos, "Previews", "Kunos");
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsGamer, "Assists", "Gamer");
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsIntermediate, "Assists", "Intermediate");
            PresetsManager.Instance.RegisterBuiltInPreset(BinaryResources.AssistsPro, "Assists", "Pro");
        }

        private static void LoadMagick() {
            try {
                ImageUtils.LoadImageMagickAssembly(AppAddonsManager.Instance.GetAddonFilename("Magick", "Magick.NET-x86.dll"));
                Logging.Write("magick test: " + ImageUtils.TestImageMagick());
            } catch (Exception e) {
                Logging.Warning("cannot load magick: " + e);
            }
        }

        private static void UnloadMagick() {
            ImageUtils.UnloadImageMagickAssembly();
        }

        private static async Task InitializeMagickAddonAsync() {
            await Task.Run(() => InitializeMagickAddon());
        }

        private static void InitializeMagickAddon() {
            if (AppAddonsManager.Instance.IsAddonEnabled("Magick")) {
                LoadMagick();
            }

            AppAddonsManager.Instance.AddonEnabled += (sender, args) => {
                if (args.AddonId != "Magick") return;
                LoadMagick();
            };
            AppAddonsManager.Instance.AddonDisabled += (sender, args) => {
                if (args.AddonId != "Magick") return;
                UnloadMagick();
            };
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
            ValuesStorage.SaveBeforeExit();
            KunosCareerProgress.SaveBeforeExit();
        }
    }
}
