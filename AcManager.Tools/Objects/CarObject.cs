using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public partial class CarObject : AcJsonObjectNew {
        public static int OptionSkinsLoadingConcurrency = 5;

        public CarObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            SkinsManager = new CarSkinsManager(Id, new AcObjectTypeDirectories(SkinsDirectory)) {
                ScanWrapper = this
            };
        }

        public override void PastLoad() {
            base.PastLoad();
            if (!Enabled) return;

            SuggestionLists.CarBrandsList.AddUnique(Brand);
            SuggestionLists.CarClassesList.AddUnique(CarClass);
            UpdateParentValues();
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.IsAffected(filename, LogoIcon)) {
                OnImageChanged(nameof(LogoIcon));
                return true;
            }

            if (FileUtils.IsAffected(filename, BrandBadge)) {
                OnImageChanged(nameof(BrandBadge));
                return true;
            }

            if (FileUtils.IsAffected(filename, UpgradeIcon)) {
                OnImageChanged(nameof(UpgradeIcon));
                return true;
            }

            var local = filename.SubstringExt(Location.Length + 1);
            if (local.StartsWith(@"skins\", true, CultureInfo.InvariantCulture)) {
                // TODO: proper skin reload
                EnsureSkinsLoaded();
                return true;
            }

            // ignoring everything else
            return true;
        }

        public override int CompareTo(AcPlaceholderNew o) {
            var c = o as CarObject;
            if (c == null) return base.CompareTo(o);

            var lhsEnabled = Enabled;
            if (lhsEnabled != c.Enabled) return lhsEnabled ? -1 : 1;

            var lhsParent = Parent;
            var rhsParent = c.Parent;

            if (lhsParent == null && rhsParent == null || lhsParent == rhsParent) {
                return DisplayName.CompareToExt(c.DisplayName);
            }

            if (lhsParent == c) return 1;
            if (rhsParent == this) return -1;
            return (lhsParent ?? this).DisplayName.CompareToExt((rhsParent ?? c).DisplayName);
        }

        protected override void Toggle() {
            if (Parent == null) {
                // TODO:
                //foreach (var car in CarsManager.Instance.List.Where(x => x.ParentId == Id)) {
                //    car.Enabled = enable;
                //}
            } else if (!Enabled && !Parent.Enabled) {
                Parent.Toggle();
            }

            base.Toggle();
        }

        #region Simple Properties
        private string _brand;
        public string Brand {
            get { return _brand; }
            set {
                if (value == _brand) return;
                _brand = value;
                OnPropertyChanged(nameof(Brand));
                Changed = true;
            }
        }

        private string _carClass;
        public string CarClass {
            get { return _carClass; }
            set {
                if (value == _carClass) return;
                _carClass = value;
                OnPropertyChanged(nameof(CarClass));
                Changed = true;
            }
        }
        #endregion

        #region Specifications
        private string _specsBhp;
        public string SpecsBhp {
            get { return _specsBhp; }
            set {
                if (value == _specsBhp) return;
                _specsBhp = value;
                OnPropertyChanged(nameof(SpecsBhp));
                OnPropertyChanged(nameof(SpecsInfoDisplay));
                Changed = true;
            }
        }

        private string _specsTorque;
        public string SpecsTorque {
            get { return _specsTorque; }
            set {
                if (value == _specsTorque) return;
                _specsTorque = value;
                OnPropertyChanged(nameof(SpecsTorque));
                OnPropertyChanged(nameof(SpecsInfoDisplay));

                Changed = true;
            }
        }

        private string _specsWeight;
        public string SpecsWeight {
            get { return _specsWeight; }
            set {
                if (value == _specsWeight) return;
                _specsWeight = value;
                OnPropertyChanged(nameof(SpecsWeight));
                OnPropertyChanged(nameof(SpecsInfoDisplay));

                Changed = true;
            }
        }

        private string _specsTopSpeed;
        public string SpecsTopSpeed {
            get { return _specsTopSpeed; }
            set {
                if (value == _specsTopSpeed) return;
                _specsTopSpeed = value;
                OnPropertyChanged(nameof(SpecsTopSpeed));
                OnPropertyChanged(nameof(SpecsInfoDisplay));

                Changed = true;
            }
        }

        private string _specsAcceleration;
        public string SpecsAcceleration {
            get { return _specsAcceleration; }
            set {
                if (value == _specsAcceleration) return;
                _specsAcceleration = value;
                OnPropertyChanged(nameof(SpecsAcceleration));
                OnPropertyChanged(nameof(SpecsInfoDisplay));

                Changed = true;
            }
        }

        private string _specsPwRatio;
        public string SpecsPwRatio {
            get { return _specsPwRatio; }
            set {
                if (value == _specsPwRatio) return;
                _specsPwRatio = value;
                OnPropertyChanged(nameof(SpecsPwRatio));
                OnPropertyChanged(nameof(SpecsInfoDisplay));

                Changed = true;
            }
        }

        public string SpecsInfoDisplay {
            get {
                var result = new StringBuilder();
                foreach (var val in new[] {
                    SpecsBhp,
                    SpecsTorque,
                    SpecsWeight,
                    SpecsPwRatio,
                    SpecsTopSpeed,
                    SpecsAcceleration
                }.Where(val => !string.IsNullOrWhiteSpace(val))) {
                    if (result.Length > 0) {
                        result.Append(", ");
                    }

                    result.Append(val);
                }

                return result.Length > 0 ? result.ToString() : null;
            }
        }

        private GraphData _specsTorqueCurve;
        public GraphData SpecsTorqueCurve {
            get { return _specsTorqueCurve; }
            set {
                if (value == _specsTorqueCurve) return;
                _specsTorqueCurve = value;
                OnPropertyChanged(nameof(SpecsTorqueCurve));
                Changed = true;
            }
        }

        private GraphData _specsPowerCurve;
        public GraphData SpecsPowerCurve {
            get { return _specsPowerCurve; }
            set {
                if (value == _specsPowerCurve) return;
                _specsPowerCurve = value;
                OnPropertyChanged(nameof(SpecsPowerCurve));
                Changed = true;
            }
        }
        #endregion

        #region Paths
        public string LogoIcon => ImageRefreshing ?? Path.Combine(Location, "logo.png");

        public string BrandBadge => ImageRefreshing ?? Path.Combine(Location, "ui", "badge.png");

        public string UpgradeIcon => ImageRefreshing ?? Path.Combine(Location, "ui", "upgrade.png");

        public string SkinsDirectory => Path.Combine(Location, "skins");

        public override string JsonFilename => Path.Combine(Location, "ui", "ui_car.json");
        #endregion

        #region Loading
        protected override void LoadData(JObject json) {
            base.LoadData(json);

            Brand = json.GetStringValueOnly("brand");

            if (string.IsNullOrWhiteSpace(Brand)) {
                AddError(AcErrorType.Data_CarBrandIsMissing);
            }

            if (Country == null && Brand != null) {
                Country = AcStringValues.CountryFromBrand(Brand);
            }

            CarClass = json.GetStringValueOnly("class");
            ParentId = json.GetStringValueOnly("parent");

            var specsObj = json["specs"] as JObject;
            SpecsBhp = specsObj?.GetStringValueOnly("bhp");
            SpecsTorque = specsObj?.GetStringValueOnly("torque");
            SpecsWeight = specsObj?.GetStringValueOnly("weight");
            SpecsTopSpeed = specsObj?.GetStringValueOnly("topspeed");
            SpecsAcceleration = specsObj?.GetStringValueOnly("acceleration");
            SpecsPwRatio = specsObj?.GetStringValueOnly("pwratio");

            var torqueCurve = json["torqueCurve"] as JArray;
            SpecsTorqueCurve = torqueCurve != null ? new GraphData(torqueCurve) : new GraphData();

            var powerCurve = json["powerCurve"] as JArray;
            SpecsPowerCurve = powerCurve != null ? new GraphData(powerCurve) : new GraphData();
        }

        protected override void LoadYear(JObject json) {
            base.LoadYear(json);
            if (!Year.HasValue) {
                Year = DataProvider.Instance.CarYears.GetValueOrDefault(Id);
            }
        }

        protected override bool TestIfKunos() {
            return base.TestIfKunos() || TestIfKunosUsingGuids(Id);
        }

        public override void SaveData(JObject json) {
            base.SaveData(json);

            json["brand"] = Brand;
            json["class"] = CarClass;

            if (ParentId != null) {
                json["parent"] = ParentId;
            } else {
                json.Remove("parent");
            }

            var specsObj = json["specs"] as JObject;
            if (specsObj == null) {
                json["specs"] = specsObj = new JObject();
            }

            specsObj["bhp"] = SpecsBhp;
            specsObj["torque"] = SpecsTorque;
            specsObj["weight"] = SpecsWeight;
            specsObj["topspeed"] = SpecsTopSpeed;
            specsObj["acceleration"] = SpecsAcceleration;
            specsObj["pwratio"] = SpecsPwRatio;

            json["torqueCurve"] = SpecsTorqueCurve?.ToJArray();
            json["powerCurve"] = SpecsPowerCurve?.ToJArray();
        }
        #endregion
    }
}
