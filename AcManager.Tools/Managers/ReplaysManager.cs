using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class ReplaysManager : AcManagerFileSpecific<ReplayObject> {
        private static ReplaysManager _instance;

        public static ReplaysManager Instance => _instance ?? (_instance = new ReplaysManager());

        private ReplaysManager() {
            SettingsHolder.Drive.PropertyChanged += OnDrivePropertyChanged;
            MultiDirectoryMode = true;
        }

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            CleanUpTemporary();
            return base.ScanOverride();
        }

        private readonly Busy _cleaningUpBusy = new Busy();
        private void CleanUpTemporary() {
            _cleaningUpBusy.TaskDelay(() => Task.Run(() => {
                var list = Directory.GetFiles(Directories.GetMainDirectory(), "__cm_tmp_*.tmp");
                foreach (var filename in list) {
                    File.Delete(filename);
                }
            }), 1000);
        }

        protected override bool FilterId(string id) {
            return !id.StartsWith("__cm_tmp_") || !id.EndsWith(".tmp");
        }

        private void OnDrivePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.DriveSettings.TryToLoadReplays)) return;
            Rescan();
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.ReplaysDirectories;

        protected override ReplayObject CreateAcObject(string id, bool enabled) {
            return new ReplayObject(this, id, enabled);
        }
    }
}
