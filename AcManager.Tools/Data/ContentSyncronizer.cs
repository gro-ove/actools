using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    public class ContentSyncronizer : NotifyPropertyChanged {
        public static ContentSyncronizer Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new ContentSyncronizer();
        }

        private TimeSpan _updatePeriod;

        private ContentSyncronizer() {
            _updatePeriod = SettingsHolder.Common.UpdatePeriod.TimeSpan;
            SettingsHolder.Common.PropertyChanged += Common_PropertyChanged;

            GetInstalledVersion();
            if (_updatePeriod != TimeSpan.Zero) {
                CheckAndUpdateIfNeeded().Forget();
            }
        }

        private CancellationTokenSource _periodicCheckCancellation;

        private void Common_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != "UpdatePeriod") return;

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
        }

        private async Task PeriodicCheckAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                await Task.Delay(_updatePeriod, token);
                await CheckAndUpdateIfNeeded();
            }
        }

        private static string VersionFromData(string data) {
            return JsonConvert.DeserializeObject<ContentManifest>(data).Version;
        }

        private void GetInstalledVersion() {
            var versionFilename = FilesStorage.Instance.CombineFilename(FilesStorage.ContentDirName, "Manifest.json");

            try {
                InstalledVersion = File.Exists(versionFilename) ? VersionFromData(File.ReadAllText(versionFilename)) : null;
            } catch (Exception e) {
                InstalledVersion = null;
                Logging.Warning("cannot read installed version: " + e);
            }
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

        public async Task CheckAndUpdateIfNeeded() {
            if (CheckingInProcess) return;

            CheckingInProcess = true;
            CheckAndUpdateIfNeededCommand.OnCanExecuteChanged();

            LatestError = null;

            await Task.Delay(500);

            try {
                GetInstalledVersion();
                var latest = await GetLatestVersion();
                if (latest == null) {
                    return;
                }

                if (InstalledVersion == null || latest.IsVersionNewerThan(InstalledVersion)) {
                    await LoadAndInstall();
                }
            } catch (Exception e) {
                LatestError = "Some unhandled error happened.";
                Logging.Warning("cannot check and update content: " + e);
            } finally {
                CheckingInProcess = false;
                CheckAndUpdateIfNeededCommand.OnCanExecuteChanged();
            }
        }

        private RelayCommand _contentCheckForUpdatesCommand;
        public RelayCommand CheckAndUpdateIfNeededCommand => _contentCheckForUpdatesCommand ?? (_contentCheckForUpdatesCommand = new RelayCommand(async o => {
            await Instance.CheckAndUpdateIfNeeded();
        }, o => !CheckingInProcess && !IsGetting));

        public class ContentManifest {
            [JsonProperty(PropertyName = "version")]
            public string Version;
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
                var data = await CmApiProvider.GetStringAsync("content/manifest");
                return data == null ? null : VersionFromData(data);
            } catch (Exception e) {
                LatestError = "Can't download information about latest version.";
                Logging.Warning("cannot get content/manifest.json: " + e);
                return null;
            } finally {
                IsGetting = false;
            }
        }

        private bool _isInstalling;

        private async Task LoadAndInstall() {
            if (_isInstalling) return;
            _isInstalling = true;

            try {
                var data = await CmApiProvider.GetDataAsync("content/latest");
                
                string installedVersion = null;
                await Task.Run(() => {
                    var location = FilesStorage.Instance.CombineFilename(FilesStorage.ContentDirName);
                    Directory.Delete(location, true);

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        installedVersion = VersionFromData(archive.GetEntry("Manifest.json").Open().ReadAsStringAndDispose());
                        archive.ExtractToDirectory(location);
                    }
                });

                InstalledVersion = installedVersion;
                Logging.Write("Content loaded: " + InstalledVersion);
            } catch (Exception e) {
                NonfatalError.Notify(@"Can't load content pack",
                        @"Make sure internet connection is working and nothing is going on in Content folder.", e);
            } finally {
                _isInstalling = false;
            }
        }

        private string _installedVersion;

        public string InstalledVersion {
            get { return _installedVersion; }
            set {
                if (Equals(value, _installedVersion)) return;
                _installedVersion = value;
                OnPropertyChanged();

                DataProvider.Instance.RefreshData();
            }
        }
    }
}
