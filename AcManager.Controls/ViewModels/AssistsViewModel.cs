using System;
using System.ComponentModel;
using AcManager.Tools.Helpers;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Full version with presets. Load-save-switch between presets-save as a preset, full
    /// package. Also, provides previews for presets!
    /// </summary>
    public class AssistsViewModel : BaseAssistsViewModel, IUserPresetable, IPresetsPreviewProvider {
        private static AssistsViewModel _instance;

        public static AssistsViewModel Instance => _instance ?? (_instance = new AssistsViewModel("qdassists"));

        public AssistsViewModel([Localizable(false)] string customKey = null) : base(customKey, false) {
            PresetableKey = customKey ?? UserPresetableKeyValue;
            Saveable.Initialize();
        }

        protected override void SaveLater() {
            base.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        #region Presetable
        bool IUserPresetable.CanBeSaved => true;

        public string PresetableKey { get; }

        string IUserPresetable.PresetableCategory => UserPresetableKeyValue;

        string IUserPresetable.DefaultPreset => ControlsStrings.AssistsPreset_Pro;

        public string ExportToPresetData() {
            return Saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            Saveable.FromSerializedString(data);
        }

        object IPresetsPreviewProvider.GetPreview(string data) {
            return new AssistsDescription { DataContext = CreateFixed(data) };
        }
        #endregion
    }
}
