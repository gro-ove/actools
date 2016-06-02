namespace AcManager.Tools.Managers.Presets {
    public interface ISavedPresetEntry {
        string DisplayName { get; }

        string Filename { get; }

        string ReadData();
    }
}