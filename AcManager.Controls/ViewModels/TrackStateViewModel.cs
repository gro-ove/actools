using System;
using System.ComponentModel;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json.Linq;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Full version with presets. Load-save-switch between presets-save as a preset, full
    /// package. Also, provides previews for presets!
    /// </summary>
    public class TrackStateViewModel : TrackStateViewModelBase, IUserPresetable, IUserPresetableDefaultPreset, IUserPresetableCustomDisplay,
            IUserPresetableCustomSorting, IPresetsPreviewProvider {
        private static TrackStateViewModel _instance;

        public static TrackStateViewModel Instance => _instance ?? (_instance = new TrackStateViewModel("qdtrackstate"));

        public TrackStateViewModel([Localizable(false)] string customKey = null) : base(customKey, false) {
            PresetableKey = customKey ?? PresetableCategory.DirectoryName;
            Saveable.Initialize();
        }

        protected override void SaveLater() {
            base.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        #region Presetable
        bool IUserPresetable.CanBeSaved => true;
        public string PresetableKey { get; }
        PresetsCategory IUserPresetable.PresetableCategory => PresetableCategory;
        string IUserPresetableDefaultPreset.DefaultPreset => "Green";

        public string ExportToPresetData() {
            return Saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            Saveable.FromSerializedString(data);
        }

        object IPresetsPreviewProvider.GetPreview(string data) {
            return new UserControls.TrackStateDescription { DataContext = CreateFixed(data) };
        }

        private static double? LoadGrip(string data) {
            try {
                var o = JObject.Parse(data);
                if (o["w"].As(false)) return -1d;
                return o["s"].As<double>();
            } catch (Exception e) {
                Logging.Error(e.Message);
                return null;
            }
        }

        string IUserPresetableCustomDisplay.GetDisplayName(string name, string data) {
            var g = LoadGrip(data);
            return g.HasValue ? g == -1d ? name : $"{name} ({g.Value * 100:F0}%)" : $"{name} (?)";
        }

        int IUserPresetableCustomSorting.Compare(string aName, string aData, string bName, string bData) {
            var aGrip = LoadGrip(aData);
            var bGrip = LoadGrip(bData);
            if (aGrip == bGrip) return string.Compare(aName, bName, StringComparison.Ordinal);
            return (aGrip ?? 0d).CompareTo(bGrip ?? 0d);
        }
        #endregion
    }
}