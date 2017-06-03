using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.Objects {
    public class TrueTypeFontObject : AcCommonSingleFileObject {
        public const string FileExtension = ".ttf";

        public override string Extension => FileExtension;

        public TrueTypeFontObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        public override bool HasData => true;
    }
}