using System;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class ReplaysManager : AcManagerFileSpecific<ReplayObject> {
        public static ReplaysManager Instance { get; private set; }

        public static ReplaysManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new ReplaysManager();
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.ReplaysDirectories;

        protected override ReplayObject CreateAcObject(string id, bool enabled) {
            return new ReplayObject(this, id, enabled);
        }
    }
}
