using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class ShowroomsManager : AcManagerNew<ShowroomObject> {
        public static ShowroomsManager Instance { get; private set; }

        public static ShowroomsManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new ShowroomsManager();
        }

        protected override ShowroomObject CreateAcObject(string id, bool enabled) {
            return new ShowroomObject(this, id, enabled);
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.ShowroomsDirectories;

        public override ShowroomObject GetDefault() {
            return GetById("showroom") ?? base.GetDefault();
        }

        protected override bool ShouldSkipFileInternal(string filename) {
            return filename.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
        }
    }
}
