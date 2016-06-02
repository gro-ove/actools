using System;

namespace AcManager.Controls {
    public interface IUserPresetable {
        bool CanBeSaved { get; }

        string UserPresetableKey { get; }

        string ExportToUserPresetData();

        event EventHandler Changed;

        void ImportFromUserPresetData(string data);
    }
}