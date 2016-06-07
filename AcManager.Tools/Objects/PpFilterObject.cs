using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.Objects {
    public class PpFilterObject : AcCommonSingleFileObject {
        public override string Extension => ".ini";

        public PpFilterObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        public override bool HasData => true;
    }
}
