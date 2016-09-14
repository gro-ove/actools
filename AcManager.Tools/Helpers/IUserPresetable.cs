using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public interface IUserPresetable {
        bool CanBeSaved { get; }

        [NotNull]
        string PresetableCategory { get; }

        [NotNull]
        string PresetableKey { get; }

        string DefaultPreset { get; }

        string ExportToPresetData();

        event EventHandler Changed;

        void ImportFromPresetData([NotNull] string data);
    }
}