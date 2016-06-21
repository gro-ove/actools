using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class CarSkinObject : AcJsonObjectNew {
        public string CarId { get; }

        public CarSkinObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
        }

        protected override bool LoadJsonOrThrow() {
            if (!File.Exists(JsonFilename)) {
                ClearData();
                Name = AcStringValues.NameFromId(Id);
                Changed = true;
                return true;
            }

            return base.LoadJsonOrThrow();
        }

        public override void Save() {
            if (HasData || string.IsNullOrEmpty(Name)) {
                base.Save();
            } else {
                var json = new JObject();
                SaveData(json);

                Changed = false;
                File.WriteAllText(JsonFilename, json.ToString());
            }
        }

        protected override void ClearData() {
            base.ClearData();
            DriverName = null;
            Team = null;
            SkinNumber = null;
            Priority = null;
        }

        public override void Reload() {
            base.Reload();
            OnImageChanged(nameof(PreviewImage));
            OnImageChanged(nameof(LiveryImage));
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.IsAffected(filename, PreviewImage)) {
                OnImageChanged(nameof(PreviewImage));
                CheckPreview();
                return true;
            }

            if (FileUtils.IsAffected(filename, LiveryImage)) {
                OnImageChanged(nameof(LiveryImage));
                CheckLivery();
                return true;
            }
            
            return true;
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        public string LiveryImage => Path.Combine(Location, "livery.png");

        public string PreviewImage => Path.Combine(Location, "preview.jpg");

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckLivery();
            CheckPreview();
        }

        private void CheckLivery() {
            ErrorIf(!File.Exists(LiveryImage), AcErrorType.CarSkin_LiveryIsMissing, Id);
        }

        private void CheckPreview() {
            ErrorIf(!File.Exists(PreviewImage), AcErrorType.CarSkin_PreviewIsMissing, Id);
        }

        public override string JsonFilename => Path.Combine(Location, "ui_skin.json");

        private string _driverName;

        [CanBeNull]
        public string DriverName {
            get { return _driverName; }
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;
                OnPropertyChanged();
            }
        }

        private string _team;

        [CanBeNull]
        public string Team {
            get { return _team; }
            set {
                if (Equals(value, _team)) return;
                _team = value;
                OnPropertyChanged();
            }
        }

        private string _skinNumber;

        [CanBeNull]
        public string SkinNumber {
            get { return _skinNumber; }
            set {
                if (Equals(value, _skinNumber)) return;
                _skinNumber = value;
                OnPropertyChanged();
            }
        }

        private int? _priority;
        
        public int? Priority {
            get { return _priority; }
            set {
                if (Equals(value, _priority)) return;
                _priority = value;
                OnPropertyChanged();
            }
        }

        private void LoadSkinRelated(JObject json) {
            DriverName = json.GetStringValueOnly("drivername")?.Trim();
            Team = json.GetStringValueOnly("team")?.Trim();
            SkinNumber = json.GetStringValueOnly("number")?.Trim();
            Priority = json.GetIntValueOnly("priority");
        }

        private void SaveSkinRelated(JObject json) {
            json["drivername"] = DriverName ?? string.Empty;
            json["team"] = Team ?? string.Empty;
            json["number"] = SkinNumber ?? string.Empty;

            if (Priority.HasValue) {
                json["priority"] = Priority.Value;
            } else {
                json.Remove("priority");
            }
        }

        protected override void SaveCountry(JObject json) {
            json["country"] = Country ?? string.Empty;
        }

        public override void PastLoad() {
            base.PastLoad();
            if (!Enabled) return;

            SuggestionLists.CarSkinTeamsList.AddUnique(Team);
        }

        protected override void LoadData(JObject json) {
            Name = json.GetStringValueOnly("skinname");
            if (string.IsNullOrWhiteSpace(Name)) {
                // more than usual case
                // AddError(AcErrorType.Data_ObjectNameIsMissing);
            }

            LoadTags(json);
            LoadCountry(json);
            LoadDescription(json);
            LoadYear(json);
            LoadVersionInfo(json);

            LoadSkinRelated(json);
        }

        public override void SaveData(JObject json) {
            json["skinname"] = Name ?? string.Empty;
            SaveCountry(json);
            SaveSkinRelated(json);

            json.Remove("tags");
            json.Remove("description");
            json.Remove("author");
            json.Remove("version");
            json.Remove("url");

            // SaveTags(json);
            // SaveDescription(json);
            // SaveYear(json);
            // SaveVersionInfo(json);
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarSkinTagsList;
        }
    }
}
