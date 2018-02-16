using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Presets {
    public static class SavedPresetEntryExtension {
        [NotNull]
        public static string ReadData([NotNull] this ISavedPresetEntry preset) {
            return preset.ReadBinaryData().ToUtf8String();
        }
    }
}