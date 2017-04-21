using System;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcCommonSingleFileObject : AcCommonObject {
        public abstract string Extension { get; }

        protected AcCommonSingleFileObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        private string _oldName;

        protected override void LoadOrThrow() {
            _oldName = Id.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            Name = _oldName;
        }

        protected virtual Task RenameAsync() {
            return RenameAsync(Name + Extension);
        }

        public override void Save() {
            if (_oldName != Name) {
                RenameAsync();
            }
        }

        public override bool HandleChangedFile(string filename) {
            return true;
        }
    }
}
