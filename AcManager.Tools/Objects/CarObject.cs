using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;
using StringBasedFilter;

namespace AcManager.Tools.Objects {
    [MoonSharpUserData]
    public sealed partial class CarObject : AcJsonObjectNew, IDraggable {
        public static int OptionSkinsLoadingConcurrency = 5;

        public CarObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            InitializeLocationsOnce();
            SkinsManager = InitializeSkins();
        }

        protected override void InitializeLocations() {
            base.InitializeLocations();
            LogoIcon = Path.Combine(Location, "logo.png");
            BrandBadge = Path.Combine(Location, @"ui", @"badge.png");
            UpgradeIcon = Path.Combine(Location, @"ui", @"upgrade.png");
            SkinsDirectory = Path.Combine(Location, "skins");
            JsonFilename = Path.Combine(Location, @"ui", @"ui_car.json");
        }

        public override string DisplayName => Name == null ? Id :
                SettingsHolder.Content.CarsYearPostfix && Year.HasValue && !Name.EndsWith(Year?.ToInvariantString()) &&
                        !AcStringValues.GetYearFromName(Name).HasValue ? $@"{Name} '{Year % 100:D2}" : Name;

        public override int? Year {
            get => base.Year;
            set {
                base.Year = value;
                if (SettingsHolder.Content.CarsYearPostfix && Loaded) {
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(ShortName));
                }
            }
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarTagsList;
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

            _errors.CollectionChanged += OnCarObjectCollectionChanged;
            _errors.Add(InnerErrors);

            if (!Enabled) return;

            // SuggestionLists.CarClassesList.AddUnique(CarClass);
            UpdateParentValues();
        }

        private void OnCarObjectCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
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
            } else if (FileUtils.IsAffected(filename, SoundbankFilename)) {
                _soundDonorId = null;
                _soundDonor = null;
                _soundDonorSet = false;
                OnPropertyChanged(nameof(SoundDonorId));
                OnPropertyChanged(nameof(SoundDonor));
            } else if (_acdDataRead && FileUtils.IsAffected(Path.Combine(Location, "data.acd"), filename)) {
                if (_acdData == null) {
                    _acdDataRead = false;
                    OnPropertyChanged(nameof(AcdData));
                } else {
                    _acdData.Refresh(null);
                }
            } else if (_acdDataRead && _acdData?.IsPacked != true && FileUtils.IsAffected(Path.Combine(Location, "data"), filename)) {
                if (_acdData == null) {
                    _acdDataRead = false;
                    OnPropertyChanged(nameof(AcdData));
                } else {
                    _acdData.Refresh(FileUtils.GetRelativePath(filename, Path.Combine(Location, "data")).ToLowerInvariant());
                }
            }

            return true;
        }

        private static int Compare(CarObject l, CarObject r) {
            var le = l.Enabled;
            return le != r.Enabled ? (le ? -1 : 1) : l.DisplayName.InvariantCompareTo(r.DisplayName);
        }

        public override int CompareTo(AcPlaceholderNew o) {
            if (o is CarObject r) {
                var tp = Parent;
                var rp = r.Parent;
                if (rp == this) return -1;
                if (tp == r) return 1;
                if (tp == rp) return Compare(this, r);
                return Compare(tp ?? this, rp ?? r);
            }

            return base.CompareTo(o);
        }

        private bool _skipRelativesToggling;

        protected override async Task ToggleOverrideAsync() {
            if (_skipRelativesToggling) {
                await base.ToggleOverrideAsync();
                return;
            }

            var enabled = Enabled;
            var parent = Parent;
            if (parent == null) {
                await base.ToggleOverrideAsync();
                foreach (var car in Children.Where(x => x.Enabled == enabled).ToList()) {
                    try {
                        car._skipRelativesToggling = true;
                        await car.ToggleOverrideAsync();
                    } finally {
                        car._skipRelativesToggling = false;
                    }
                }
            } else if (!enabled && !parent.Enabled) {
                try {
                    parent._skipRelativesToggling = true;
                    await parent.ToggleOverrideAsync();
                } finally {
                    parent._skipRelativesToggling = false;
                }
                await base.ToggleOverrideAsync();
            } else {
                await base.ToggleOverrideAsync();
            }
        }

        #region Simple Properties
        private string _brand;

        [CanBeNull]
        public string Brand {
            get => _brand;
            set {
                value = value?.Trim();

                if (Equals(value, _brand)) return;
                _brand = value;

                ErrorIf(string.IsNullOrEmpty(value) && HasData, AcErrorType.Data_CarBrandIsMissing);

                if (Loaded) {
                    OnPropertyChanged(nameof(Brand));
                    OnPropertyChanged(nameof(ShortName));
                    Changed = true;
                }
            }
        }

        private string _carClass;

        [CanBeNull]
        public string CarClass {
            get => _carClass;
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

        [NotNull]
        public string ShortName => DisplayName.ApartFromFirst(Brand).TrimStart();
        #endregion

        #region Specifications
        private string _specsBhp;

        [CanBeNull]
        public string SpecsBhp {
            get => _specsBhp;
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
            get => _specsTorque;
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
            get => _specsWeight;
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

        public double? GetWidthValue() {
            double v;
            if (!FlexibleParser.TryParseDouble(_specsWeight, out v)) {
                return null;
            }

            // specially for cars with wrongly placed delimiter
            return v < 4 ? v * 1e3 : v;
        }

        private string _specsTopSpeed;

        [CanBeNull]
        public string SpecsTopSpeed {
            get => _specsTopSpeed;
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
            get => _specsAcceleration;
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
            get => _specsPwRatio;
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

        private double? _rpmMaxValue;

        public double GetRpmMaxValue() {
            return _rpmMaxValue ?? (_rpmMaxValue = SpecsTorqueCurve?.MaxX ?? SpecsPowerCurve?.MaxX ?? double.NaN).Value;
        }

        public double GetSpecsPwRatioValue() {
            double value;
            if (!FlexibleParser.TryParseDouble(_specsPwRatio, out value)) return double.NaN;

            if (SettingsHolder.Content.CarsProperPwRatio && PwUsualFormat.IsMatch(_specsPwRatio)) {
                value = 1 / value;
            }

            return value;
        }

        private static readonly Regex FixMissingSpace = new Regex(@"(\d)([a-z])/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PwUsualFormat = new Regex(@"(\d|\b)kg\s*/", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private string FixSpec(string v) {
            if (v == null || !SettingsHolder.Content.CarsFixSpecs) return v;
            return FixMissingSpace.Replace(v, @"\1 \2");
        }

        [CanBeNull]
        public string SpecsInfoDisplay {
            get {
                var result = new StringBuilder();

                var pwRatio = SpecsPwRatio;
                if (pwRatio != null && SettingsHolder.Content.CarsProperPwRatio && PwUsualFormat.IsMatch(pwRatio)) {
                    double value;
                    pwRatio = FlexibleParser.TryParseDouble(pwRatio, out value) ? $"{1 / value:F2} hp/kg" : pwRatio;
                }

                foreach (var val in new[] {
                    SpecsBhp,
                    SpecsTorque,
                    SpecsWeight,
                    pwRatio,
                    SpecsTopSpeed,
                    SpecsAcceleration
                }.Where(val => val?.Length > 0 && char.IsDigit(val[0]))) {
                    if (result.Length > 0) {
                        result.Append(@", ");
                    }

                    result.Append(FixSpec(val).Replace(' ', ' '));
                }

                return result.Length > 0 ? result.ToString() : null;
            }
        }

        private GraphData _specsTorqueCurve;

        [CanBeNull]
        public GraphData SpecsTorqueCurve {
            get => _specsTorqueCurve;
            set {
                if (value == _specsTorqueCurve) return;
                _specsTorqueCurve = value;
                _rpmMaxValue = null;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsTorqueCurve));
                    Changed = true;
                }
            }
        }

        private GraphData _specsPowerCurve;

        [CanBeNull]
        public GraphData SpecsPowerCurve {
            get => _specsPowerCurve;
            set {
                if (value == _specsPowerCurve) return;
                _specsPowerCurve = value;
                _rpmMaxValue = null;

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
            return /*base.TestIfKunos() ||*/ TestIfKunosUsingGuids(Id);
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

        public const string DraggableFormat = "Data-CarObject";
        string IDraggable.DraggableFormat => DraggableFormat;

        #region Packing
        public new static string OptionCanBePackedFilter = "k-&!id:`^ad_`";

        private static readonly Lazy<IFilter<CarObject>> CanBePackedFilterObj = new Lazy<IFilter<CarObject>>(() =>
                Filter.Create(CarObjectTester.Instance, OptionCanBePackedFilter));

        public override bool CanBePacked() {
            return CanBePackedFilterObj.Value.Test(this);
        }

        public class CarPackerParams : AcCommonObjectPackerParams {
            public bool PackData { get; set; } = true;
            public bool IncludeTemplates { get; set; } = true;
        }

        private class CarPacker : AcCommonObjectPacker<CarObject, CarPackerParams> {
            protected override string GetBasePath(CarObject t) {
                return $"content/cars/{t.Id}";
            }

            protected override IEnumerable PackOverride(CarObject t) {
                // Fonts
                yield return Add(t.AcdData?.GetIniFile("digital_instruments.ini")
                                  .Values.Select(x => x.GetNonEmpty("FONT")?.ToLowerInvariant())
                                  .NonNull()?.Select(FontsManager.Instance.GetByAcId).Where(x => x?.Author != AuthorKunos));

                // Driver models
                var driver = DriverModelsManager.Instance.GetByAcId(
                        t.AcdData?.GetIniFile("driver3d.ini")["MODEL"].GetNonEmpty("NAME") ?? "");
                if (driver != null && driver.Author != AuthorKunos) {
                    yield return Add(driver);
                }

                // Various files
                yield return Add("body_shadow.png", "tyre_?_shadow.png", "collider.kn5", "driver_base_pos.knh", "logo.png");
                yield return Add("animations/*.ksanim");
                yield return Add("sfx/GUIDs.txt", $"sfx/{t.Id}.bank");
                yield return Add("texture/*", "texture/flames/*.dds", "texture/flames/*.png");
                yield return Add("ui/badge.png", "ui/ui_car.json", "ui/upgrade.png", "ui/cm_*.json");

                if (Params.IncludeTemplates) {
                    yield return Add("templates/*");
                }

                var textureNames = Kn5.FromFile(FileUtils.GetMainCarFilename(t.Location, t.AcdData),
                        SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance).TexturesData.Keys.ToList();
                yield return Add(textureNames.Select(x => $"skins/*/{x}"));
                yield return Add("skins/*/livery.png", "skins/*/preview.jpg", "skins/*/ui_skin.json", "skins/*/cm_*.json");

                yield return Add("data.acd");
                if (!Has("data.acd")) {
                    if (Params.PackData) {
                        var dataDirectory = Path.Combine(t.Location, "data");
                        var acd = Acd.FromDirectory(dataDirectory);
                        using (var s = new MemoryStream()) {
                            acd.Save(dataDirectory, s);
                            yield return AddBytes("data.acd", s.ToArray());
                        }
                    } else {
                        yield return Add("data/*");
                    }
                }

                var data = DataWrapper.FromCarDirectory(t.Location);
                yield return Add(data.GetIniFile("lods.ini").GetSections("LOD").Select(x => x.GetNonEmpty("FILE")));
            }

            protected override PackedDescription GetDescriptionOverride(CarObject t) {
                return new PackedDescription(t.Id, t.Name,
                    new Dictionary<string, string> {
                        ["Version"] = t.Version,
                        ["Made by"] = t.Author,
                        ["Webpage"] = t.Url,
                    }, CarsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new CarPacker();
        }
        #endregion
    }
}
