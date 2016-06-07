using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class PpFilterObject : AcCommonSingleFileObject {
        public override string Extension => ".ini";

        public PpFilterObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        public override bool HasData => true;

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            _preparedForEditing = false;
        }

        public override void Save() {
            if (_preparedForEditing && Changed && SaveEdited()) {
                Changed = false;
            }

            base.Save();
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase) && _preparedForEditing) {
                Content = File.ReadAllText(Location);
            }

            return true;
        }

        private string _content;

        public string Content {
            get { return _content; }
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
                Logging.Write("[PpFilterObject] Can’t load: " + e);
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
                NonfatalError.Notify("Can’t save PP filter", "Make sure its file could be overwritten.", e);
                return false;
            }
        }

        public void SetChanged(bool changed) {
            Changed = changed;
        }
    }
}
