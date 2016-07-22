using System;
using AcManager.Tools.Helpers;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Full version with presets. Load-save-switch between presets-save as a preset, full
    /// package. Also, provides previews for presets!
    /// </summary>
    public class AssistsViewModel : BaseAssistsViewModel, IUserPresetable, IPreviewProvider {
        private static AssistsViewModel _instance;

        public static AssistsViewModel Instance => _instance ?? (_instance = new AssistsViewModel());

        private readonly string _customKey;

        public AssistsViewModel(string customKey = null) : base(customKey, false) {
            _customKey = customKey ?? UserPresetableKeyValue;
            Saveable.Initialize();
        }

        protected override void SaveLater() {
            base.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        #region Presetable
        bool IUserPresetable.CanBeSaved => true;

        string IUserPresetable.PresetableKey => _customKey;

        string IUserPresetable.PresetableCategory => UserPresetableKeyValue;

        string IUserPresetable.DefaultPreset => ControlsStrings.AssistsPreset_Pro;

        string IUserPresetable.ExportToPresetData() {
            return Saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        void IUserPresetable.ImportFromPresetData(string data) {
            Saveable.FromSerializedString(data);
        }

        object IPreviewProvider.GetPreview(string data) {
            return new AssistsDescription { DataContext = CreateFixed(data) };
        }
        #endregion
    }
}
