using System;

namespace AcManager.Tools.Helpers {
    public interface IUserPresetable {
        bool CanBeSaved { get; }

        string PresetableCategory { get; }

        string PresetableKey { get; }

        string DefaultPreset { get; }

        string ExportToPresetData();

        event EventHandler Changed;

        void ImportFromPresetData(string data);
    }
}