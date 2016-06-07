using System.Diagnostics;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.Objects {
    public class PpFilterObject : AcCommonSingleFileObject {
        public override string Extension => ".ini";

        public PpFilterObject(IFileAcManager manager, string fileName, bool enabled)
                : base(manager, fileName, enabled) {
            Debug.WriteLine("FileName: " + FileName);
        }

        public override bool HasData => true;
    }
}
