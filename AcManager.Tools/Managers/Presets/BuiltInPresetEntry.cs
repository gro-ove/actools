using System;
using System.Text;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers.Presets {
    internal class BuiltInPresetEntry : Displayable, ISavedPresetEntry {
        private readonly string _extension;

        public byte[] Data { get; }

        public string BaseDirectory { get; }

        public string Filename { get; }

        public BuiltInPresetEntry(string baseDirectory, string filename, string extension, byte[] data) {
            _extension = extension;
            BaseDirectory = baseDirectory;
            Filename = filename;
            Data = data;
        }

        public string ReadData() {
            return Encoding.UTF8.GetString(Data);
        }

        public void SetParent(string baseDirectory) {}

        private string _displayName;

        public override string DisplayName {
            get {
                if (_displayName != null) return _displayName;
                var start = BaseDirectory.Length + 1;
                return _displayName =
                        Filename.Substring(start, Filename.Length - start - _extension.Length);
            }
        }

        public override string ToString() {
            return DisplayName;
        }

        public bool Equals(ISavedPresetEntry other) {
            return other != null && string.Equals(Filename, other.Filename, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object other) {
            return Equals(other as ISavedPresetEntry);
        }

        protected bool Equals(BuiltInPresetEntry other) {
            return other != null && string.Equals(Filename, other.Filename, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return Filename?.GetHashCode() ?? 0;
        }
    }
}