using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers.Presets {
    internal class SavedPresetEntry : Displayable, ISavedPresetEntry {
        public string BaseDirectory { get; private set; }

        public string Filename { get; private set; }

        public override string DisplayName {
            get {
                if (_displayName != null) return _displayName;
                var start = BaseDirectory.Length + 1;
                return _displayName = Filename.Substring(start, Filename.Length - start - PresetsManager.FileExtension.Length);
            }
        }

        public SavedPresetEntry(string baseDirectory, string filename) {
            BaseDirectory = baseDirectory;
            Filename = filename;
        }

        public string ReadData() {
            return FileUtils.ReadAllText(Filename);
        }

        private string _displayName;

        public override string ToString() {
            return DisplayName;
        }
    }
}