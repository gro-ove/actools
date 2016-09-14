using System;
using System.ComponentModel;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry : INotifyPropertyChanged, IEquatable<ISavedPresetEntry> {
        [NotNull]
        string DisplayName { get; }

        [NotNull]
        string Filename { get; }

        [NotNull]
        string ReadData();
    }
}