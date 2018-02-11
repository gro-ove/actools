using System;
using System.ComponentModel;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry : INotifyPropertyChanged, IEquatable<ISavedPresetEntry> {
        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string Filename { get; }

        [NotNull]
        byte[] ReadBinaryData();

        void SetParent(string baseDirectory);
    }

    public static class SavedPresetEntryExtension {
        [NotNull]
        public static string ReadData([NotNull] this ISavedPresetEntry preset) {
            return preset.ReadBinaryData().ToUtf8String();
        }
    }
}