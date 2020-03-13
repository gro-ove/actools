using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class PpFilterObject : AcCommonSingleFileObject {
        public const string FileExtension = ".ini";

        public override string Extension => FileExtension;

        public PpFilterObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        public string AcId => Id.ApartFromLast(FileExtension);

        public override bool HasData => true;

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            _preparedForEditing = false;
        }

        public override Task SaveAsync() {
            if (_preparedForEditing && Changed && SaveEdited()) {
                Changed = false;
            }

            return base.SaveAsync();
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase) && _preparedForEditing) {
                Content = File.ReadAllText(Location);
            }

            return true;
        }

        private string _content;

        public string Content {
            get => _content;
            set {
                if (Equals(value, _content)) return;
                _content = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private bool _preparedForEditing;

        public void PrepareForEditing() {
            if (_preparedForEditing) return;
            _preparedForEditing = true;

            var changed = Changed;
            try {
                Content = File.ReadAllText(Location);
                RemoveError(AcErrorType.Data_IniIsMissing);
            } catch (Exception e) {
                Logging.Write("Can’t load: " + e);
                AddError(AcErrorType.Data_IniIsMissing, Id);
            } finally {
                Changed = changed;
            }
        }

        private bool SaveEdited() {
            try {
                File.WriteAllText(Location, Content);
                return true;
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.PpFilterObject_CannotSave, ToolsStrings.PpFilterObject_MakeSureCouldBeOverwritten, e);
                return false;
            }
        }

        #region Packing
        private class PpFilterPacker : AcCommonObjectPacker<PpFilterObject> {
            protected override string GetBasePath(PpFilterObject t) {
                return "system/cfg/ppfilters";
            }

            protected override IEnumerable PackOverride(PpFilterObject t) {
                yield return AddFilename(Path.GetFileName(t.Location), t.Location);
            }

            protected override PackedDescription GetDescriptionOverride(PpFilterObject t) {
                return new PackedDescription(t.Id, t.Name, null, PpFiltersManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new PpFilterPacker();
        }
        #endregion
    }
}
