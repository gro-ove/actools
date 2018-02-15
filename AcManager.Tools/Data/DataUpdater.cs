using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    public class DataUpdater : BaseUpdater {
        public static DataUpdater Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new DataUpdater();
        }

        public DataUpdater() : base(GetInstalledVersion()) { }

        protected override TimeSpan GetUpdatePeriod() {
            return TimeSpan.FromDays(3);
        }

        protected override void OnCommonSettingsChanged(object sender, PropertyChangedEventArgs e) {
            // Do nothing
            // base.OnCommonSettingsChanged(sender, e);
        }

        [CanBeNull]
        private static string VersionFromData([CanBeNull] string data) {
            return data == null ? null : JsonConvert.DeserializeObject<ContentManifest>(data).Version;
        }

        [CanBeNull]
        private static string GetInstalledVersion() {
            var onlineFiltersFilename = FilesStorage.Instance.Combine(FilesStorage.DataDirName, ContentCategory.OnlineFilters, @"Quick Filters.json");
            if (!File.Exists(onlineFiltersFilename)) {
                Logging.Warning("Quick Filters are missing");
                return @"0";
            }

            var versionFilename = FilesStorage.Instance.Combine(FilesStorage.DataDirName, @"Manifest.json");

            try {
                return File.Exists(versionFilename) ? VersionFromData(File.ReadAllText(versionFilename)) : null;
            } catch (Exception e) {
                Logging.Warning("Cannot read installed version: " + e);
                return null;
            }
        }

        protected override async Task<bool> CheckAndUpdateIfNeededInner() {
            InstalledVersion = GetInstalledVersion();
            var latest = await GetLatestVersion();

            Logging.Write($"Installed: {InstalledVersion}, latest: {latest}");
            return latest != null && (InstalledVersion == null || latest.IsVersionNewerThan(InstalledVersion)) && await LoadAndInstall();
        }

        public class ContentManifest {
            [JsonProperty(PropertyName = @"version")]
            public string Version;
        }

        private async Task<string> GetLatestVersion() {
            if (IsGetting) return null;
            IsGetting = true;

            try {
                var data = await CmApiProvider.GetStringAsync("data/manifest");
                if (data == null) {
                    LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                    return null;
                }

                return VersionFromData(data);
            } catch (Exception e) {
                LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                Logging.Warning("Cannot get data/manifest.json: " + e);
                return null;
            } finally {
                IsGetting = false;
            }
        }

        private bool _isInstalling;

        private async Task<bool> LoadAndInstall() {
            if (_isInstalling) return false;
            _isInstalling = true;

            try {
                var data = await CmApiProvider.GetDataAsync("data/latest");
                if (data == null) throw new InformativeException(ToolsStrings.AppUpdater_CannotLoad, ToolsStrings.Common_MakeSureInternetWorks);

                string installedVersion = null;
                await Task.Run(() => {
                    var location = FilesStorage.Instance.Combine(FilesStorage.DataDirName);

                    for (var i = 10; i >= 0; i--) {
                        try {
                            CleanUp(location);
                            break;
                        } catch (IOException e) {
                            if (i == 0) throw;
                            Logging.Warning(e.Message);
                            Thread.Sleep(30);
                        } catch (UnauthorizedAccessException e) {
                            if (i == 0) throw;
                            Logging.Warning(e.Message);
                            Thread.Sleep(30);
                        }
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        installedVersion = VersionFromData(archive.GetEntry(@"Manifest.json")?.Open().ReadAsStringAndDispose());
                        archive.ExtractToDirectory(location);
                    }
                });

                InstalledVersion = installedVersion;
                Logging.Write("Data loaded: " + InstalledVersion);
                return true;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.ContentSyncronizer_CannotLoadContent, ToolsStrings.ContentSyncronizer_CannotLoadContent_Commentary, e);
            } finally {
                _isInstalling = false;
            }

            return false;

            void CleanUp(string location) {
                if (Directory.Exists(location)) {
                    Directory.Delete(location, true);
                }
            }
        }

        protected override void OnUpdated() {
            base.OnUpdated();
            DataProvider.Instance.RefreshData();
        }
    }
}
