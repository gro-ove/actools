using System.Text;

namespace AcManager.Tools.Managers.Presets {
    internal class BuiltInPresetEntry : ISavedPresetEntry {
        public byte[] Data { get; }

        public string BaseDirectory { get; }

        public string Filename { get; internal set; }

        public BuiltInPresetEntry(string baseDirectory, string filename, byte[] data) {
            BaseDirectory = baseDirectory;
            Filename = filename;
            Data = data;
        }

        public string ReadData() {
            return Encoding.UTF8.GetString(Data);
        }

        private string _displayName;
        
        public string DisplayName {
            get {
                if (_displayName != null) return _displayName;
                var start = BaseDirectory.Length + 1;
                return _displayName =
                        Filename.Substring(start, Filename.Length - start - PresetsManager.FileExtension.Length);
            }
        }

        public override string ToString() {
            return DisplayName;
        }
    }
}