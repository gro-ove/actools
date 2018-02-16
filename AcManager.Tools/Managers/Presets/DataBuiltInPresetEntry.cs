using System.IO;

namespace AcManager.Tools.Managers.Presets {
    internal class DataBuiltInPresetEntry : SavedPresetEntry {
        private readonly string _actualFilename;
        public override bool IsBuiltIn => true;

        public DataBuiltInPresetEntry(string baseDirectory, string extension, string virtualFilename, string actualFilename)
                : base(baseDirectory, extension, virtualFilename) {
            _actualFilename = actualFilename;
        }

        public override byte[] ReadBinaryData() {
            return File.ReadAllBytes(_actualFilename);
        }
    }
}