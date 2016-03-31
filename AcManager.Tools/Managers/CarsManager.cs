using System;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class CarsManager : AcManagerNew<CarObject> {
        public static CarsManager Instance { get; private set; }

        public static CarsManager Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new CarsManager();
        }

        public override AcObjectTypeDirectories Directories => AcRootDirectory.Instance.CarsDirectories;

        public override CarObject GetDefault() {
            return GetById("abarth500") ?? base.GetDefault();
        }

        protected override bool ShouldSkipFileInternal(string filename) {
            return filename.EndsWith(".ksamin", StringComparison.OrdinalIgnoreCase) ||
                   filename.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ||
                   filename.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
                   filename.EndsWith("preview_original.jpg", StringComparison.OrdinalIgnoreCase);
        }

        protected override CarObject CreateAcObject(string id, bool enabled) {
            return new CarObject(this, id, enabled);
        }
    }
}
