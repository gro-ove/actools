using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationParams {
        public static readonly ContentInstallationParams Default = new ContentInstallationParams();

        public bool AllowExecutables { get; set; }

        [CanBeNull]
        public string CarId { get; set; }

        [CanBeNull]
        public string FallbackId { get; set; }

        [CanBeNull]
        public string Checksum { get; set; }

        [CanBeNull]
        public string DisplayName { get; set; }

        [CanBeNull]
        public string InformationUrl { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        // CUP-related
        public CupContentType? CupType { get; set; }

        [CanBeNull]
        public string[] IdsToUpdate { get; set; }

        [CanBeNull]
        public string Author { get; set; }

        public bool PreferCleanInstallation { get; set; }

        public bool SyncDetails { get; set; }

        public async Task PostInstallation(IProgress<AsyncProgressEntry> progress, CancellationToken token) {
            if (!CupType.HasValue || IdsToUpdate == null) return;

            var manager = CupClient.Instance?.GetAssociatedManager(CupType.Value);
            if (manager == null) return;

            // TODO: Make it firmer
            progress.Report(new AsyncProgressEntry("Syncing versions…", 0.9999));
            await Task.Delay(1000);
            foreach (var cupSupportedObject in IdsToUpdate.Select(x => manager.GetObjectById(x) as ICupSupportedObject).NonNull()) {
                Logging.Debug($"Set values: {cupSupportedObject}={Version}");
                cupSupportedObject.SetValues(Author, InformationUrl, Version);
            }
        }
    }
}