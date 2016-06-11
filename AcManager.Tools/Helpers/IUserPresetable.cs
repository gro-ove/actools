using System;

namespace AcManager.Tools.Helpers {
    public interface IUserPresetable {
        bool CanBeSaved { get; }

        string UserPresetableKey { get; }

        string ExportToUserPresetData();

        event EventHandler Changed;

        void ImportFromUserPresetData(string data);
    }
}