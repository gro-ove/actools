using System;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Presets {
    internal class BuiltInPresetEntry : Displayable, ISavedPresetEntry {
        private readonly string _extension;
        private readonly string _baseDirectory;
        private readonly byte[] _data;
        public string VirtualFilename { get; }
        public bool IsBuiltIn => true;

        public BuiltInPresetEntry([NotNull] string baseDirectory, [NotNull] string filename, [NotNull] string extension, byte[] data) {
            _extension = extension;
            _baseDirectory = baseDirectory;
            _data = data;
            VirtualFilename = filename;

            _displayName = Lazier.Create(GetDisplayName);
        }

        private string GetDisplayName() {
            var start = _baseDirectory.Length + 1;
            return VirtualFilename.Substring(start, VirtualFilename.Length - start - _extension.Length);
        }

        public byte[] ReadBinaryData() {
            return _data;
        }

        public void SetParent(string baseDirectory) {}

        private readonly Lazier<string> _displayName;

        public override string DisplayName => _displayName.RequireValue;

        public override string ToString() {
            return DisplayName;
        }

        public bool Equals(ISavedPresetEntry other) {
            return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object other) {
            return Equals(other as ISavedPresetEntry);
        }

        protected bool Equals(BuiltInPresetEntry other) {
            return other != null && string.Equals(VirtualFilename, other.VirtualFilename, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() {
            return VirtualFilename.GetHashCode();
        }
    }
}