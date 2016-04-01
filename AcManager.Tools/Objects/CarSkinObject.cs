using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class CarSkinObject : AcJsonObjectNew {
        public string CarId { get; }

        public CarSkinObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        public string LiveryImage => Path.Combine(Location, "livery.png");

        public string PreviewImage => Path.Combine(Location, "preview.jpg");

        public override string JsonFilename => Path.Combine(Location, "ui_skin.json");

        private string _driverName;

        public string DriverName {
            get { return _driverName; }
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;
                OnPropertyChanged();
            }
        }

        private string _team;

        public string Team {
            get { return _team; }
            set {
                if (Equals(value, _team)) return;
                _team = value;
                OnPropertyChanged();
            }
        }

        private string _skinNumber;

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
            json["drivername"] = DriverName;
            json["team"] = Team;
            json["number"] = SkinNumber;

            if (Priority.HasValue) {
                json["priority"] = Priority.Value;
            } else {
                json.Remove("priority");
            }
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
            json["skinname"] = Name ?? "";
            SaveTags(json);
            SaveCountry(json);
            SaveDescription(json);
            SaveYear(json);
            SaveVersionInfo(json);

            SaveSkinRelated(json);
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarSkinTagsList;
        }
    }
}
