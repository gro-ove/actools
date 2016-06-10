using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.Objects {
    public class PythonAppObject : AcCommonObject {
        public PythonAppObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {}

        protected override void LoadOrThrow() {
            Name = Id;
        }

        public override bool HasData => true;

        public override void Save() {
            if (Name != Id) {
                FileAcManager.Rename(Id, Name, Enabled);
            }
        }
    }
}
