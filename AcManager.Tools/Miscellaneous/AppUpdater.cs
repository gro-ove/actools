using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public class AppUpdater : BaseUpdater {
        [Localizable(false)]
        private static string Branch => AppKeyHolder.IsAllRight && SettingsHolder.Common.UpdateToNontestedVersions ? "latest" : "tested";

        public static AppUpdater Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);

            PreviousVersion = ValuesStorage.GetString(KeyPreviousVersion);
            if (PreviousVersion?.IsVersionOlderThan(BuildInformation.AppVersion) == true) {
                JustUpdated = true;
                ValuesStorage.Set(KeyPreviousVersion, BuildInformation.AppVersion);
            }

            Instance = new AppUpdater();
        }

        private AppUpdater() : base(BuildInformation.AppVersion) {}

        protected override void OnCommonSettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.Common.UpdatePeriod):
                    RestartPeriodicCheck();
                    break;

                case nameof(SettingsHolder.Common.UpdateToNontestedVersions):
                    if (!SettingsHolder.Common.UpdateToNontestedVersions || UpdatePeriod == TimeSpan.Zero) return;
                    CheckAndUpdateIfNeeded().Forget();
                    break;
            }
        }

        private static string VersionFromData(string data) {
            return JsonConvert.DeserializeObject<AppManifest>(data).Version;
        }

        private bool _isSupported = true;

        public bool IsSupported {
            get { return _isSupported; }
            set {
                if (Equals(value, _isSupported)) return;
                _isSupported = value;
                OnPropertyChanged();
            }
        }

        public override Task CheckAndUpdateIfNeeded() {
            if (!MainExecutingFile.IsPacked) {
                LatestError = ToolsStrings.AppUpdater_UnpackedVersionMessage;
                IsSupported = false;
                return Task.Delay(0);
            }

            if (BuildInformation.Platform != @"x86") {
                LatestError = "Non-x86 version doesn’t support auto-updating yet.";
                IsSupported = false;
                return Task.Delay(0);
            }

            return base.CheckAndUpdateIfNeeded();
        }

        protected override async Task<bool> CheckAndUpdateIfNeededInner() {
            return (await GetLatestVersion())?.IsVersionNewerThan(BuildInformation.AppVersion) == true && await LoadAndPrepare();
        }

        private async Task<string> GetLatestVersion() {
            if (IsGetting) return null;
            IsGetting = true;

            try {
                var data = await CmApiProvider.GetStringAsync($"app/manifest/{Branch}");
                if (data == null) {
                    LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                    return null;
                }

                return VersionFromData(data);
            } catch (Exception e) {
                LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                Logging.Warning("Cannot get app/manifest.json: " + e);
                return null;
            } finally {
                IsGetting = false;
            }
        }

        public class AppManifest {
            [JsonProperty(PropertyName = @"version")]
            public string Version;
        }

        private string _updateIsReady;

        public string UpdateIsReady {
            get { return _updateIsReady; }
            set {
                if (Equals(value, _updateIsReady)) return;
                _updateIsReady = value;
                OnPropertyChanged();
                _finishUpdateCommand?.OnCanExecuteChanged();
            }
        }

        private bool _isPreparing;

        private async Task<bool> LoadAndPrepare() {
            if (!MainExecutingFile.IsPacked) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_CannotUpdateApp, ToolsStrings.AppUpdater_UnpackedVersionMessage);
                LatestError = ToolsStrings.AppUpdater_UnpackedVersionMessage;
                return false;
            }

            if (_isPreparing) return false;
            _isPreparing = true;
            UpdateIsReady = null;

            try {
                var data = await CmApiProvider.GetDataAsync($"app/get/{Branch}");
                if (data == null) throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);

                string preparedVersion = null;
                await Task.Run(() => {
                    if (File.Exists(UpdateLocation)) {
                        File.Delete(UpdateLocation);
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        preparedVersion = VersionFromData(archive.GetEntry(@"Manifest.json").Open().ReadAsStringAndDispose());

                        archive.GetEntry(@"Content Manager.exe").ExtractToFile(UpdateLocation);
                        Logging.Write($"New version {preparedVersion} was extracted to “{UpdateLocation}”");
                    }
                });

                UpdateIsReady = preparedVersion;
                return true;
            } catch (UnauthorizedAccessException) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_AccessIsDenied,
                        ToolsStrings.AppUpdater_AccessIsDenied_Commentary);
                LatestError = ToolsStrings.AppUpdater_AccessIsDenied_Short;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.AppUpdater_CannotLoad,
                        ToolsStrings.AppUpdater_CannotLoad_Commentary, e);
                LatestError = ToolsStrings.AppUpdater_CannotLoadShort;
            } finally {
                _isPreparing = false;
            }

            return false;
        }

        private ProperCommand _finishUpdateCommand;

        public ICommand FinishUpdateCommand => _finishUpdateCommand ?? (_finishUpdateCommand = new ProperCommand(o => {
            RunUpdateExeAndExitIfExists();
        }, o => UpdateIsReady != null));

        private const string ExecutableExtension = ".exe";

        private const string UpdatePostfix = ".update" + ExecutableExtension;

        public static string UpdateLocation => MainExecutingFile.Location.ApartFromLast(ExecutableExtension, StringComparison.OrdinalIgnoreCase) + UpdatePostfix;

        public static bool OnStartup(string[] args) {
            try {
                if (MainExecutingFile.Location.EndsWith(UpdatePostfix)) {
                    InstallAndRunNewVersion();
                    return true;
                }

                if (File.Exists(UpdateLocation)) {
                    if (FileVersionInfo.GetVersionInfo(UpdateLocation).FileVersion.IsVersionNewerThan(BuildInformation.AppVersion)) {
                        Thread.Sleep(200);
                        RunUpdateExeAndExitIfExists();
                        return true;
                    }

                    CleanUpUpdateExeAsync().Forget();
                }

                return false;
            } catch (Exception e) {
                MessageBox.Show(string.Format(ToolsStrings.AppUpdater_CannotUpdate_Message, e.Message), ToolsStrings.AppUpdater_UpdateFailed,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void RunUpdateExeAndExitIfExists() {
            if (!File.Exists(UpdateLocation)) return;
            Logging.Write($"Starting “{UpdateLocation}”…");
            ProcessExtension.Start(UpdateLocation, Environment.GetCommandLineArgs().Skip(1));
            Environment.Exit(0);
        }

        private static async Task CleanUpUpdateExeAsync() {
            for (var i = 0; i < 10; i++) {
                try {
                    File.Delete(UpdateLocation);
                    break;
                } catch (Exception) {
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
        }

        private static void InstallAndRunNewVersion() {
            /* will be replaced this file */
            var originalFilename = MainExecutingFile.Location.ApartFromLast(UpdatePostfix, StringComparison.OrdinalIgnoreCase) + ExecutableExtension;

            /* if file already exists */
            if (File.Exists(originalFilename)) {
                /* ten attempts, five seconds */
                for (var i = 0; i < 10; i++) {
                    try {
                        File.Delete(originalFilename);
                        break;
                    } catch (Exception) {
                        Thread.Sleep(500);
                    }
                }

                /* if we couldn’t delete file normally, let’s kill any process with this name */
                if (File.Exists(originalFilename)) {
                    try {
                        foreach (var process in Process.GetProcessesByName(Path.GetFileName(originalFilename))) {
                            process.Kill();
                        }
                    } catch (Exception) {
                        // ignored
                    }

                    /* four attempts, two seconds */
                    for (var i = 0; i < 4; i++) {
                        try {
                            File.Delete(originalFilename);
                        } catch (Exception) {
                            Thread.Sleep(500);
                        }
                    }
                }

                if (File.Exists(originalFilename)) {
                    MessageBox.Show(
                            string.Format(ToolsStrings.AppUpdater_CannotUpdate_HelpNeeded, Path.GetFileName(originalFilename),
                                    Path.GetFileName(MainExecutingFile.Location)),
                            ToolsStrings.AppUpdater_UpdateFailed, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            File.Copy(MainExecutingFile.Location, originalFilename);
            ProcessExtension.Start(originalFilename, Environment.GetCommandLineArgs().Skip(1));
            Environment.Exit(0);
        }

        private const string KeyPreviousVersion = "AppUpdater:PreviousVersion";

        public static bool JustUpdated { get; private set; }

        public static string PreviousVersion { get; private set; }

        public static IEnumerable<ChangelogEntry> LoadChangelog() {
            return InternalUtils.LoadChangelog(CmApiProvider.UserAgent);
        }
    }
}
