using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    [MoonSharpUserData]
    public partial class CarObject : AcJsonObjectNew {
        public static int OptionSkinsLoadingConcurrency = 5;

        public CarObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            SkinsManager = new CarSkinsManager(Id, new InheritingAcDirectories(manager.Directories, SkinsDirectory)) {
                ScanWrapper = this
            };
            SkinsManager.Created += SkinsManager_Created;
        }

        private CompositeObservableCollection<IAcError> _errors;

        public override ObservableCollection<IAcError> Errors => _errors;

        private void SkinsManager_Created(object sender, AcObjectEventArgs<CarSkinObject> args) {
            if (!Application.Current.Dispatcher.CheckAccess()) {
                throw new InvalidOperationException(Resources.UIThreadRequired);
            }

            _errors.Add(args.AcObject.Errors);
            args.AcObject.AcObjectOutdated += AcObject_AcObjectOutdated;
        }

        private void AcObject_AcObjectOutdated(object sender, EventArgs e) {
            var ac = (AcCommonObject)sender;
            ac.AcObjectOutdated -= AcObject_AcObjectOutdated;
            _errors.Remove(ac.Errors);
        }

        public override void PastLoad() {
            base.PastLoad();

            _errors = new CompositeObservableCollection<IAcError>();
            _errors.CollectionChanged += CarObject_CollectionChanged;
            _errors.Add(InnerErrors);

            if (!Enabled) return;

            SuggestionLists.CarBrandsList.AddUnique(Brand);
            SuggestionLists.CarClassesList.AddUnique(CarClass);
            UpdateParentValues();
        }

        private void CarObject_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            OnPropertyChanged(nameof(HasErrors));
        }

        protected override void OnAcObjectOutdated() {
            foreach (var obj in Skins) {
                obj.Outdate();
            }

            base.OnAcObjectOutdated();
        }

        protected override void ClearData() {
            base.ClearData();

            Brand = null;
            Country = null;
            CarClass = null;
            ParentId = null;

            SpecsBhp = null;
            SpecsTorque = null;
            SpecsWeight = null;
            SpecsTopSpeed = null;
            SpecsAcceleration = null;
            SpecsPwRatio = null;

            SpecsTorqueCurve = null;
            SpecsPowerCurve = null;
        }

        public override void Reload() {
            OnImageChanged(nameof(LogoIcon));
            OnImageChanged(nameof(BrandBadge));
            OnImageChanged(nameof(UpgradeIcon));

            SkinsManager.Rescan();
            base.Reload();
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;

            if (FileUtils.IsAffected(filename, LogoIcon)) {
                OnImageChanged(nameof(LogoIcon));
            } else if (FileUtils.IsAffected(filename, BrandBadge)) {
                OnImageChanged(nameof(BrandBadge));
            } else if (FileUtils.IsAffected(filename, UpgradeIcon)) {
                OnImageChanged(nameof(UpgradeIcon));
            }

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

        [CanBeNull]
        public string Brand {
            get { return _brand; }
            set {
                if (value == _brand) return;
                _brand = value;

                if (value == null && HasData) {
                    AddError(AcErrorType.Data_CarBrandIsMissing);
                } else {
                    RemoveError(AcErrorType.Data_CarBrandIsMissing);
                }

                OnPropertyChanged(nameof(Brand));
                Changed = true;
            }
        }

        private string _carClass;

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

        [CanBeNull]
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

            SpecsTorqueCurve = new GraphData(json["torqueCurve"] as JArray);
            SpecsPowerCurve = new GraphData(json["powerCurve"] as JArray);
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
