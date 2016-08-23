using System;
using System.ComponentModel;

namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry : INotifyPropertyChanged, IEquatable<ISavedPresetEntry> {
        string DisplayName { get; }

        string Filename { get; }

        string ReadData();
    }
}