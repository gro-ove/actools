using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    [MoonSharpUserData]
    public sealed partial class CarObject : AcJsonObjectNew {
        public static int OptionSkinsLoadingConcurrency = 5;

        public CarObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            InitializeLocationsOnce();
            SkinsManager = new CarSkinsManager(Id, new InheritingAcDirectories(manager.Directories, SkinsDirectory), Skins_CollectionReady) {
                ScanWrapper = this
            };
            SkinsManager.Created += SkinsManager_Created;
        }

        protected override void InitializeLocations() {
            base.InitializeLocations();

            LogoIcon = Path.Combine(Location, "logo.png");
            BrandBadge = Path.Combine(Location, @"ui", @"badge.png");
            UpgradeIcon = Path.Combine(Location, @"ui", @"upgrade.png");
            SkinsDirectory = Path.Combine(Location, "skins");
            JsonFilename = Path.Combine(Location, @"ui", @"ui_car.json");
        }

        private void Skins_CollectionReady(object sender, EventArgs e) {
            var any = SkinsManager.GetDefault();
            ErrorIf(any == null, AcErrorType.CarSkins_SkinsAreMissing);
            if (any == null) {
                SelectedSkin = null;
            } else if (SelectedSkin == null) {
                SelectedSkin = any;
            }
        }

        public override string DisplayName => Name == null ? Id :
                SettingsHolder.Content.CarsYearPostfix && Year.HasValue && !AcStringValues.GetYearFromName(Name).HasValue
                        ? $"{Name} '{Year % 100:D2}" : Name;

        public override int? Year {
            get { return base.Year; }
            set {
                base.Year = value;
                if (SettingsHolder.Content.CarsYearPostfix && Loaded) {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarTagsList;
        }

        private readonly CompositeObservableCollection<IAcError> _errors = new CompositeObservableCollection<IAcError>();

        public override ObservableCollection<IAcError> Errors => _errors;

        private void SkinsManager_Created(object sender, AcObjectEventArgs<CarSkinObject> args) {
            _errors.Add(args.AcObject.Errors);
            args.AcObject.AcObjectOutdated += AcObject_AcObjectOutdated;
        }

        private void AcObject_AcObjectOutdated(object sender, EventArgs e) {
            var ac = (AcCommonObject)sender;
            ac.AcObjectOutdated -= AcObject_AcObjectOutdated;
            _errors.Remove(ac.Errors);
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckBrandBadge();
            CheckUpgradeIcon();
        }

        private void CheckBrandBadge() {
            ErrorIf(!File.Exists(BrandBadge), AcErrorType.Car_BrandBadgeIsMissing);
        }

        private void CheckUpgradeIcon() {
            ErrorIf(ParentId != null && !File.Exists(UpgradeIcon), AcErrorType.Car_UpgradeIconIsMissing);
        }

        public override void PastLoad() {
            base.PastLoad();
            
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
            foreach (var obj in SkinsManager.LoadedOnly) {
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
            OnImageChangedValue(LogoIcon);
            OnImageChangedValue(BrandBadge);
            OnImageChangedValue(UpgradeIcon);

            SkinsManager.Rescan();
            base.Reload();
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;
            
            if (FileUtils.IsAffected(filename, LogoIcon)) {
                OnImageChangedValue(LogoIcon);
            } else if (FileUtils.IsAffected(filename, BrandBadge)) {
                CheckBrandBadge();
                OnImageChangedValue(BrandBadge);
            } else if (FileUtils.IsAffected(filename, UpgradeIcon)) {
                CheckUpgradeIcon();
                OnImageChangedValue(UpgradeIcon);
            } else if (FileUtils.IsAffected(filename, Path.Combine(Location, "data.acd"))) {
                UpdateAcdData();
            }

            return true;
        }

        private static int Compare(CarObject l, CarObject r) {
            var le = l.Enabled;
            return le != r.Enabled ? (le ? -1 : 1) : l.DisplayName.InvariantCompareTo(r.DisplayName);
        }

        public override int CompareTo(AcPlaceholderNew o) {
            var r = o as CarObject;
            if (r == null) return base.CompareTo(o);

            var tp = Parent;
            var rp = r.Parent;
            if (rp == this) return -1;
            if (tp == r) return 1;
            if (tp == rp) return Compare(this, r);
            return Compare(tp ?? this, rp ?? r);
        }

        private bool _skipRelativesToggling;

        protected override void Toggle() {
            if (_skipRelativesToggling) {
                base.Toggle();
                return;
            }

            var enabled = Enabled;
            var parent = Parent;
            if (parent == null) {
                base.Toggle();
                foreach (var car in Children.Where(x => x.Enabled == enabled).ToList()) {
                    try {
                        car._skipRelativesToggling = true;
                        car.Toggle();
                    } finally {
                        car._skipRelativesToggling = false;
                    }
                }
            } else if (!enabled && !parent.Enabled) {
                try {
                    parent._skipRelativesToggling = true;
                    parent.Toggle();
                } finally {
                    parent._skipRelativesToggling = false;
                }
                base.Toggle();
            } else {
                base.Toggle();
            }
        }

        #region Simple Properties
        private string _brand;

        [CanBeNull]
        public string Brand {
            get { return _brand; }
            set {
                value = value?.Trim();

                if (Equals(value, _brand)) return;
                _brand = value;
                
                ErrorIf(string.IsNullOrEmpty(value) && HasData, AcErrorType.Data_CarBrandIsMissing);

                if (Loaded) {
                    OnPropertyChanged(nameof(Brand));
                    Changed = true;

                    SuggestionLists.RebuildCarBrandsList();
                }
            }
        }

        private string _carClass;

        [CanBeNull]
        public string CarClass {
            get { return _carClass; }
            set {
                if (value == _carClass) return;
                _carClass = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(CarClass));
                    Changed = true;

                    SuggestionLists.RebuildCarClassesList();
                }
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

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsBhp));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsTorque;

        [CanBeNull]
        public string SpecsTorque {
            get { return _specsTorque; }
            set {
                if (value == _specsTorque) return;
                _specsTorque = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsTorque));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsWeight;

        [CanBeNull]
        public string SpecsWeight {
            get { return _specsWeight; }
            set {
                if (value == _specsWeight) return;
                _specsWeight = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsWeight));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsTopSpeed;

        [CanBeNull]
        public string SpecsTopSpeed {
            get { return _specsTopSpeed; }
            set {
                if (value == _specsTopSpeed) return;
                _specsTopSpeed = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsTopSpeed));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsAcceleration;

        [CanBeNull]
        public string SpecsAcceleration {
            get { return _specsAcceleration; }
            set {
                if (value == _specsAcceleration) return;
                _specsAcceleration = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsAcceleration));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsPwRatio;

        [CanBeNull]
        public string SpecsPwRatio {
            get { return _specsPwRatio; }
            set {
                if (value == _specsPwRatio) return;
                _specsPwRatio = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsPwRatio));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
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
                        result.Append(@", ");
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

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsTorqueCurve));
                    Changed = true;
                }
            }
        }

        private GraphData _specsPowerCurve;

        [CanBeNull]
        public GraphData SpecsPowerCurve {
            get { return _specsPowerCurve; }
            set {
                if (value == _specsPowerCurve) return;
                _specsPowerCurve = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsPowerCurve));
                    Changed = true;
                }
            }
        }
        #endregion

        #region Paths
        public string LogoIcon { get; private set; }

        public string BrandBadge { get; private set; }

        public string UpgradeIcon { get; private set; }

        public string SkinsDirectory { get; private set; }
        #endregion

        #region Loading
        [Localizable(false)]
        protected override void LoadData(JObject json) {
            base.LoadData(json);

            Brand = json.GetStringValueOnly("brand");
            if (string.IsNullOrEmpty(Brand)) {
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

            SpecsTorqueCurve = new GraphData(json["torqueCurve"] as JArray);
            SpecsPowerCurve = new GraphData(json["powerCurve"] as JArray);
        }

        protected override void LoadYear(JObject json) {
            Year = json.GetIntValueOnly("year");
            if (Year.HasValue) return;

            int year;
            if (DataProvider.Instance.CarYears.TryGetValue(Id, out year)) {
                Year = year;
            } else if (Name != null) {
                Year = AcStringValues.GetYearFromName(Name) ?? AcStringValues.GetYearFromId(Name);
            }
        }

        protected override bool TestIfKunos() {
            return base.TestIfKunos() || TestIfKunosUsingGuids(Id);
        }

        [Localizable(false)]
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
