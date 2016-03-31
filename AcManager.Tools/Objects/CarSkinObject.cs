using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class CarSkinObject : AcJsonObjectNew {
        public string CarId { get; }

        public CarSkinObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        public string LiveryImage => Path.Combine(Location, "livery.png");

        public string PreviewImage => Path.Combine(Location, "preview.jpg");

        public override string JsonFilename => Path.Combine(Location, "ui_skin.json");

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
        }

        public override void SaveData(JObject json) {
            json["skinname"] = Name ?? "";
            SaveTags(json);
            SaveCountry(json);
            SaveDescription(json);
            SaveYear(json);
            SaveVersionInfo(json);
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarSkinTagsList;
        }
    }
}
