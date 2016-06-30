using System;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class ReplaysManager : AcManagerFileSpecific<ReplayObject> {
        public static ReplaysManager Instance { get; private set; }

        public static ReplaysManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new ReplaysManager();
        }

        private ReplaysManager() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
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
