using System;
using System.IO;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Managers.Presets {
    internal class SavedPresetEntry : Displayable, ISavedPresetEntry, IShortDisplayable {
        private readonly string _baseDirectory;
        private string _displayBaseDirectory;
        private readonly string _extension;
        public string VirtualFilename { get; }
        public virtual bool IsBuiltIn => false;

        public SavedPresetEntry(string baseDirectory, string extension, string filename) {
            _baseDirectory = baseDirectory;
            _displayBaseDirectory = baseDirectory;
            _extension = extension;
            VirtualFilename = filename;

            _displayName = Lazier.Create(GetDisplayName);
            _shortDisplayName = Lazier.Create(GetShortDisplayName);
        }

        private string GetDisplayName() {
            var start = _baseDirectory.Length + 1;
            return VirtualFilename.Substring(start, VirtualFilename.Length - start - _extension.Length);
        }

        private string GetShortDisplayName() {
            var start = _displayBaseDirectory.Length + 1;
            return VirtualFilename.Substring(start, VirtualFilename.Length - start - _extension.Length);
        }

        public virtual byte[] ReadBinaryData() {
            return File.ReadAllBytes(VirtualFilename);
        }

        private readonly Lazier<string> _displayName, _shortDisplayName;

        public override string DisplayName => _displayName.RequireValue;
        public string ShortDisplayName => _shortDisplayName.RequireValue;

        public void SetParent(string baseDirectory) {
            _shortDisplayName.Reset();
            _displayBaseDirectory = baseDirectory;
            OnPropertyChanged(nameof(ShortDisplayName));
        }

        public override string ToString() {
            return DisplayName;
        }

        public bool Equals(ISavedPresetEntry other) {
            return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object other) {
            return Equals(other as ISavedPresetEntry);
        }

        protected bool Equals(SavedPresetEntry other) {
            return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return VirtualFilename.GetHashCode();
        }
    }
}