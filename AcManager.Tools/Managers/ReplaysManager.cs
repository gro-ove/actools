using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class ReplaysManager : AcManagerFileSpecific<ReplayObject> {
        private static ReplaysManager _instance;

        public static ReplaysManager Instance => _instance ?? (_instance = new ReplaysManager());

        private ReplaysManager() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
            MultiDirectoryMode = true;
        }

        private void Drive_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.DriveSettings.TryToLoadReplays)) return;
            Rescan();
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.ReplaysDirectories;

        protected override ReplayObject CreateAcObject(string id, bool enabled) {
            return new ReplayObject(this, id, enabled);
        }
    }
}
