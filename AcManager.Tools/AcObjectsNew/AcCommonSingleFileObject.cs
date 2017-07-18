using System;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract class AcCommonSingleFileObject : AcCommonObject {
        public abstract string Extension { get; }

        protected AcCommonSingleFileObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        protected string OldName;

        protected override void LoadOrThrow() {
            OldName = Id.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            Name = OldName;
        }

        protected virtual string GetNewId([NotNull] string newName) {
            return newName + Extension;
        }

        protected async Task RenameAsync() {
            var name = Name;
            if (string.IsNullOrWhiteSpace(name)) return;

            var oldName = OldName;
            if (!await RenameAsync(GetNewId(name))) {
                var c = Changed;
                Name = oldName;
                Changed = c;
            } else {
                OldName = name;
            }
        }

        public override void Save() {
            if (OldName != Name) {
                RenameAsync().Forget();
            }
        }

        public override bool HandleChangedFile(string filename) {
            return true;
        }
    }
}
