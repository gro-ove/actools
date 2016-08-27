using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Miscellaneous {
    public class LocaleUpdater : BaseUpdater {
        public static LocaleUpdater Instance { get; private set; }

        public static void Initialize(string loadedVersion) {
            Debug.Assert(Instance == null);
            Instance = new LocaleUpdater(loadedVersion);
        }

        public LocaleUpdater(string loadedVersion) : base(loadedVersion) { }

        protected override TimeSpan GetUpdatePeriod() {
            return SettingsHolder.Locale.UpdatePeriod.TimeSpan;
        }

        protected override void SetListener() {
            SettingsHolder.Locale.PropertyChanged += OnCommonSettingsChanged;
        }
        protected override void OnCommonSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.LocaleSettings.UpdatePeriod)) return;
            RestartPeriodicCheck();
        }

        protected override void FirstCheck() {
            if (!SettingsHolder.Locale.UpdateOnStart) {
                base.FirstCheck();
            }
        }

        protected override bool CanBeUpdated() {
            return InstalledVersion != null && base.CanBeUpdated();
        }

        protected override async Task<bool> CheckAndUpdateIfNeededInner() {
            if (InstalledVersion == null) return false;

            var data = await CmApiProvider.GetDataAsync($"locales/update/{SettingsHolder.Locale.LocaleName}/{InstalledVersion}");
            if (data == null) {
                LatestError = ToolsStrings.BaseUpdater_CannotDownloadInformation;
                Logging.Warning("Cannot get locales/update");
                return false;
            }

            if (data.Length == 0) {
                return false;
            }

            try {
                LocalePackageManifest manifest;
                using (var memory = new MemoryStream(data))
                using (var updateZip = new ZipArchive(memory)) {
                    manifest = LocalePackageManifest.FromArchive(updateZip);
                    if (manifest == null) throw new Exception("Manifest is missing");
                }

                var package = FilesStorage.Instance.GetFilename("Locales", manifest.Id + ".pak");
                await FileUtils.WriteAllBytesAsync(package, data);
                Logging.Write("Locale updated");

                InstalledVersion = manifest.Version;
                return true;
            } catch (Exception e) {
                Logging.Warning("Cannot update locale: " + e);
                return false;
            }
        }

        public async Task<string> InstallCustom(string id, IProgress<double?> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            var destination = FilesStorage.Instance.GetDirectory("Locales", id);

            var data = await CmApiProvider.GetDataAsync(@"locales/get/base", progress, cancellation);
            if (cancellation.IsCancellationRequested || data == null) return null;

            progress?.Report(null);
            using (var memory = new MemoryStream(data))
            using (var updateZip = new ZipArchive(memory)) {
                foreach (var entry in updateZip.Entries) {
                    using (var stream = entry.Open()) {
                        var filename = Path.Combine(destination, entry.Name);
                        if (File.Exists(filename)) continue;

                        await FileUtils.WriteAllBytesAsync(filename, stream, cancellation);
                        if (cancellation.IsCancellationRequested) return null;
                    }
                }
            }

            return destination;
        }
    }
}