using System.Collections;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;

namespace AcManager.Tools.Objects {
    public class TrueTypeFontObject : AcCommonSingleFileObject {
        public const string FileExtension = ".ttf";

        public override string Extension => FileExtension;

        public TrueTypeFontObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        public override bool HasData => true;

        #region Packing
        private class TrueTypeFontPacker : AcCommonObjectPacker<TrueTypeFontObject> {
            protected override string GetBasePath(TrueTypeFontObject t) {
                return "system/cfg/ppfilters";
            }

            protected override IEnumerable PackOverride(TrueTypeFontObject t) {
                yield return AddFilename(Path.GetFileName(t.Location), t.Location);
            }

            protected override PackedDescription GetDescriptionOverride(TrueTypeFontObject t) {
                return new PackedDescription(t.Id, t.Name, null, TrueTypeFontsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new TrueTypeFontPacker();
        }
        #endregion
    }
}