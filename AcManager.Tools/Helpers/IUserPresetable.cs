using System;
using AcManager.Tools.Managers.Presets;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public interface IUserPresetable {
        bool CanBeSaved { get; }

        [NotNull]
        string PresetableKey { get; }

        [NotNull]
        PresetsCategory PresetableCategory { get; }

        event EventHandler Changed;

        void ImportFromPresetData([NotNull] string data);

        [CanBeNull]
        string ExportToPresetData();
    }

    public interface IPresetsPreviewProvider {
        object GetPreview(string serializedData);
    }

    public interface IUserPresetableDefaultPreset {
        [CanBeNull]
        string DefaultPreset { get; }
    }

    public interface IUserPresetableCustomDisplay {
        [NotNull]
        string GetDisplayName([NotNull] string name, [NotNull] string data);
    }

    public interface IUserPresetableCustomSorting {
        int Compare([NotNull] string aName, [NotNull] string aData, [NotNull] string bName, [NotNull] string bData);
    }
}