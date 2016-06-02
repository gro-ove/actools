using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public class AppUpdater : NotifyPropertyChanged {
        private static string Branch => AppKeyHolder.IsAllRight && SettingsHolder.Common.UpdateToNontestedVersions ? "latest" : "tested";

        public static AppUpdater Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new AppUpdater();
        }

        private TimeSpan _updatePeriod;

        private AppUpdater() {
            _updatePeriod = SettingsHolder.Common.UpdatePeriod.TimeSpan;
            SettingsHolder.Common.PropertyChanged += Common_PropertyChanged;

            if (_updatePeriod != TimeSpan.Zero) {
                CheckAndUpdateIfNeeded().Forget();
            }
        }

        private CancellationTokenSource _periodicCheckCancellation;

        private void Common_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.Common.UpdatePeriod):
                    if (_periodicCheckCancellation != null) {
                        _periodicCheckCancellation.Cancel();
                        _periodicCheckCancellation = null;
                    }

                    var oldValue = _updatePeriod;
                    _updatePeriod = SettingsHolder.Common.UpdatePeriod.TimeSpan;
                    if (oldValue == TimeSpan.Zero) {
                        CheckAndUpdateIfNeeded().Forget();
                    }

                    if (_updatePeriod == TimeSpan.Zero) return;
                    _periodicCheckCancellation = new CancellationTokenSource();
                    PeriodicCheckAsync(_periodicCheckCancellation.Token).Forget();
                    break;

                case nameof(SettingsHolder.Common.UpdateToNontestedVersions):
                    if (!SettingsHolder.Common.UpdateToNontestedVersions || _updatePeriod == TimeSpan.Zero) return;
                    CheckAndUpdateIfNeeded().Forget();

                    if (_periodicCheckCancellation != null) {
                        _periodicCheckCancellation.Cancel();
                        _periodicCheckCancellation = null;
                    }

                    _periodicCheckCancellation = new CancellationTokenSource();
                    PeriodicCheckAsync(_periodicCheckCancellation.Token).Forget();
                    break;
            }
        }

        private async Task PeriodicCheckAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(_updatePeriod, token);
                await CheckAndUpdateIfNeeded();
            }
        }

        private static string VersionFromData(string data) {
            return JsonConvert.DeserializeObject<AppManifest>(data).Version;
        }

        private bool _checkingInProcess;

        public bool CheckingInProcess {
            get { return _checkingInProcess; }
            set {
                if (Equals(value, _checkingInProcess)) return;
                _checkingInProcess = value;
                OnPropertyChanged();
                _contentCheckForUpdatesCommand?.OnCanExecuteChanged();
            }
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

        public async Task CheckAndUpdateIfNeeded() {
            if (!MainExecutingFile.IsPacked) {
                LatestError = "Unpacked version doesn’t support auto-updating.";
                IsSupported = false;
                return;
            }

            if (CheckingInProcess) return;

            CheckingInProcess = true;
            CheckAndPrepareIfNeededCommand.OnCanExecuteChanged();

            LatestError = null;

            await Task.Delay(500);

            try {
                var latest = await GetLatestVersion();
                if (latest == null) {
                    return;
                }

                Logging.Write($"[APPUPDATED] Latest version: {latest} (current: {BuildInformation.AppVersion})");

                if (latest.IsVersionNewerThan(BuildInformation.AppVersion)) {
                    await LoadAndPrepare();
                }
            } catch (Exception e) {
                LatestError = "Some unhandled error happened.";
                Logging.Warning("[APPUPDATED] Cannot check and update app: " + e);
            } finally {
                CheckingInProcess = false;
                CheckAndPrepareIfNeededCommand.OnCanExecuteChanged();
            }
        }

        private string _latestError;

        public string LatestError {
            get { return _latestError; }
            set {
                if (Equals(value, _latestError)) return;
                _latestError = value;
                OnPropertyChanged();
            }
        }

        private bool _isGetting;

        public bool IsGetting {
            get { return _isGetting; }
            set {
                if (Equals(value, _isGetting)) return;
                _isGetting = value;
                OnPropertyChanged();
                _contentCheckForUpdatesCommand?.OnCanExecuteChanged();
            }
        }

        private async Task<string> GetLatestVersion() {
            if (IsGetting) return null;
            IsGetting = true;

            try {
                var data = await CmApiProvider.GetStringAsync($"app/manifest/{Branch}");
                return data == null ? null : VersionFromData(data);
            } catch (Exception e) {
                LatestError = "Can’t download information about latest version.";
                Logging.Warning("[APPUPDATED] Cannot get app/manifest.json: " + e);
                return null;
            } finally {
                IsGetting = false;
            }
        }

        private RelayCommand _contentCheckForUpdatesCommand;
        public RelayCommand CheckAndPrepareIfNeededCommand => _contentCheckForUpdatesCommand ?? (_contentCheckForUpdatesCommand = new RelayCommand(async o => {
            await Instance.CheckAndUpdateIfNeeded();
        }, o => !CheckingInProcess && !IsGetting));

        public class AppManifest {
            [JsonProperty(PropertyName = "version")]
            public string Version;
        }

        private string _updateIsReady;

        public string UpdateIsReady {
            get { return _updateIsReady; }
            set {
                if (Equals(value, _updateIsReady)) return;
                _updateIsReady = value;
                OnPropertyChanged();
                FinishUpdateCommand.OnCanExecuteChanged();
            }
        }

        private bool _isPreparing;

        private async Task LoadAndPrepare() {
            if (!MainExecutingFile.IsPacked) {
                NonfatalError.Notify(@"Can’t update app", "Sadly, unpacked version doesn’t support auto-updating.");
                LatestError = "Unpacked version doesn’t support auto-updating.";
                return;
            }

            if (_isPreparing) return;
            _isPreparing = true;
            UpdateIsReady = null;

            try {
                var data = await CmApiProvider.GetDataAsync($"app/get/{Branch}");

                string preparedVersion = null;
                await Task.Run(() => {
                    if (File.Exists(UpdateLocation)) {
                        File.Delete(UpdateLocation);
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        preparedVersion = VersionFromData(archive.GetEntry("Manifest.json").Open().ReadAsStringAndDispose());

                        archive.GetEntry("Content Manager.exe").ExtractToFile(UpdateLocation);
                        Logging.Write($"[APPUPDATED] New version {preparedVersion} was extracted to “{UpdateLocation}”");
                    }
                });

                UpdateIsReady = preparedVersion;
            } catch (UnauthorizedAccessException) {
                NonfatalError.Notify(@"Access is denied",
                        @"Can’t update app due to lack of permissions. Please, update it manually.");
                LatestError = "Can’t update app due to lack of permissions.";
            } catch (Exception e) {
                NonfatalError.Notify(@"Can’t load the new version",
                        @"Make sure internet connection is working and app has write permissions to its folder.", e);
                LatestError = "Can’t load the new version.";
            } finally {
                _isPreparing = false;
            }
        }

        private RelayCommand _loadAndInstallCommand;

        public RelayCommand FinishUpdateCommand => _loadAndInstallCommand ?? (_loadAndInstallCommand = new RelayCommand(o => {
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
                MessageBox.Show($"Can’t process update: {e.Message}. Try to install a new version manually, sorry.", "Update failed", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                return false;
            }
        }

        private static void RunUpdateExeAndExitIfExists() {
            if (!File.Exists(UpdateLocation)) return;
            Logging.Write($"starting “{UpdateLocation}”…");
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
                    MessageBox.Show($"Can’t remove original file “{Path.GetFileName(originalFilename)}” to install update. " +
                            $"Try to remove it manually and then run “{Path.GetFileName(MainExecutingFile.Location)}”.", "Update failed", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    return;
                }
            }

            File.Copy(MainExecutingFile.Location, originalFilename);
            ProcessExtension.Start(originalFilename, Environment.GetCommandLineArgs().Skip(1));
            Environment.Exit(0);
        }
    }
}
