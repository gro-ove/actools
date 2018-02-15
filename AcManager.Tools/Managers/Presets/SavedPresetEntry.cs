using System;
using System.IO;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Managers.Presets {
    internal class SavedPresetEntry : Displayable, ISavedPresetEntry, IShortDisplayable {
        private readonly string _extension;

        public string BaseDirectory { get; }

        public string Filename { get; }

        private string _displayName;

        public override string DisplayName {
            get {
                if (_displayName != null) return _displayName;
                var start = BaseDirectory.Length + 1;
                return _displayName = Filename.Substring(start, Filename.Length - start - _extension.Length);
            }
        }

        private string _displayBaseDirectory;
        private string _shortDisplayName;

        public string ShortDisplayName {
            get {
                if (_shortDisplayName != null) return _shortDisplayName;
                var start = _displayBaseDirectory.Length + 1;
                return _shortDisplayName = Filename.Substring(start, Filename.Length - start - _extension.Length);
            }
        }

        public SavedPresetEntry(string baseDirectory, string extension, string filename) {
            Logging.Debug(baseDirectory);
            BaseDirectory = baseDirectory;
            _displayBaseDirectory = baseDirectory;
            _extension = extension;
            Filename = filename;
        }

        public byte[] ReadBinaryData() {
            return File.ReadAllBytes(Filename);
        }

        public void SetParent(string baseDirectory) {
            _shortDisplayName = null;
            _displayBaseDirectory = baseDirectory;
            OnPropertyChanged(nameof(ShortDisplayName));
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

        protected bool Equals(SavedPresetEntry other) {
            return other != null && string.Equals(Filename, other.Filename, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return Filename.GetHashCode();
        }
    }
}