using System.ComponentModel;

namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry : INotifyPropertyChanged {
        string DisplayName { get; }

        string Filename { get; }

        string ReadData();
    }
}