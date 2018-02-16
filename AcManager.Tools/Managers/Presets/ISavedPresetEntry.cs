using System;
using System.ComponentModel;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry : INotifyPropertyChanged, IEquatable<ISavedPresetEntry> {
        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string VirtualFilename { get; }

        bool IsBuiltIn { get; }

        [NotNull]
        byte[] ReadBinaryData();

        void SetParent(string baseDirectory);
    }
}