﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class CarSkinObject : AcJsonObjectNew, IDraggable {
        [NotNull]
        public string CarId { get; }

        private string _nameCacheKey;
        private readonly Lazy<string> _nameFromId;

        public CarSkinObject([NotNull] string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
            _nameCacheKey = $@"{carId}:{id}";
            _nameFromId = new Lazy<string>(() => {
                var cut = Regex.Replace(Id, @"^\d\d?_", "");
                if (string.IsNullOrEmpty(cut)) cut = Id;
                return AcStringValues.NameFromId(cut);
            });
        }

        public string NameFromId => _nameFromId.Value;

        private bool _fullDetailsRequired;

        public void FullDetailsRequired() {
            if (_fullDetailsRequired) return;
            _fullDetailsRequired = true;
            Load();
            PastLoad();
        }

        protected override void CommonPropertyRequested() {
            FullDetailsRequired();
        }

        public override void Load() {
            if (SettingsHolder.Content.SkinsCacheNames) {
                InitializeLocationsOnce();
                if (!_fullDetailsRequired) {
                    var name = CacheStorage.Get<string>(_nameCacheKey);
                    if (name != null) {
                        ClearErrors();
                        ClearData();
                        Loaded = false;
                        Name = name;
                        Changed = false;
                        Loaded = true;
                        return;
                    }
                }
                base.Load();
                CacheStorage.Set(_nameCacheKey, Name);
            } else {
                base.Load();
            }
        }

        protected override bool LoadJsonOrThrow() {
            if (!File.Exists(JsonFilename)) {
                ClearData();
                Name = string.Empty;
                Changed = true;
                return true;
            }
            return base.LoadJsonOrThrow();
        }

        public override Task SaveAsync() {
            FullDetailsRequired();

            var json = new JObject();
            SaveData(json);
            CacheStorage.Set(_nameCacheKey, Name);

            Changed = false;
            using (CarsManager.Instance.IgnoreChanges()) {
                File.WriteAllText(JsonFilename, JsonConvert.SerializeObject(json, Formatting.Indented, new JsonSerializerSettings {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include,
                    DefaultValueHandling = DefaultValueHandling.Include,
                    Culture = CultureInfo.InvariantCulture
                }));
            }

            return Task.Delay(0);
        }

        protected override void ClearData() {
            base.ClearData();
            DriverName = null;
            Team = null;
            SkinNumber = GetSkinNumberFromId();
            SkinNumberFromId = true;
            Priority = null;
        }

        public override void Reload() {
            base.Reload();
            OnImageChangedValue(PreviewImage);
            OnImageChangedValue(LiveryImage);
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.IsAffectedBy(PreviewImage, filename)) {
                OnImageChangedValue(PreviewImage);
                CheckPreview();
                return true;
            }

            if (FileUtils.IsAffectedBy(LiveryImage, filename)) {
                OnImageChangedValue(LiveryImage);
                CheckLivery();
                return true;
            }

            return true;
        }

        public override string DisplayName => SettingsHolder.Content.CarSkinsDisplayId ? Id
                : string.IsNullOrWhiteSpace(Name) ? _nameFromId.Value : Name;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, "ui_skin.json");
            LiveryImage = Path.Combine(Location, "livery.png");

            var previewImage = SettingsHolder.Content.CarSkinsUsePngPreview ? Path.Combine(Location, "preview.png") : null;
            if (previewImage == null || !File.Exists(previewImage)) {
                previewImage = Path.Combine(Location, "preview.jpg");
            }
            PreviewImage = previewImage;
        }

        public string LiveryImage { get; private set; }
        public string PreviewImage { get; private set; }

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

        private string _driverName;

        [CanBeNull]
        public string DriverName {
            get {
                FullDetailsRequired();
                return _driverName;
            }
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    SuggestionLists.RebuildCarSkinDriverNamesList();
                }
            }
        }

        private string _team;

        [CanBeNull]
        public string Team {
            get {
                FullDetailsRequired();
                return _team;
            }
            set {
                if (Equals(value, _team)) return;
                _team = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    SuggestionLists.RebuildCarSkinTeamsList();
                }
            }
        }

        private bool _skinNumberFromId;

        public bool SkinNumberFromId {
            get => _skinNumberFromId;
            set => Apply(value, ref _skinNumberFromId);
        }

        private string _skinNumber;

        [CanBeNull]
        public string SkinNumber {
            get {
                FullDetailsRequired();
                return _skinNumber;
            }
            set {
                if (Equals(value, _skinNumber)) return;
                _skinNumber = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    SkinNumberFromId = false;
                }
            }
        }

        private int? _priority;

        public int? Priority {
            get {
                FullDetailsRequired();
                return _priority;
            }
            set {
                if (Equals(value, _priority)) return;
                _priority = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string GetSkinNumberFromId() {
            var id = Id;
            var start = 0;
            for (var i = 0; i < id.Length && i < 4; i++) {
                var c = id[i];
                if (c == '0' && start == i) {
                    start++;
                } else if (!char.IsDigit(c)) {
                    return i == 0 ? null : start == i ? @"0" : id.Substring(start, i - start);
                }
            }
            return null;
        }

        private void LoadSkinRelated(JObject json) {
            DriverName = json.GetStringValueOnly("drivername")?.Trim();
            Team = json.GetStringValueOnly("team")?.Trim();

            var skinNumber = json.GetStringValueOnly("number")?.Trim();
            if (skinNumber != null) {
                SkinNumber = skinNumber;
                SkinNumberFromId = false;
            } else {
                SkinNumber = GetSkinNumberFromId();
                SkinNumberFromId = true;
            }

            Priority = json.GetIntValueOnly("priority");

            var carSkins = DataProvider.Instance.KunosSkins.GetValueOrDefault(CarId);
            if (carSkins != null && Array.IndexOf(carSkins, Id) != -1) {
                Author = AuthorKunos;
            }
        }

        private void SaveSkinRelated(JObject json) {
            json[@"drivername"] = DriverName ?? string.Empty;
            json[@"team"] = Team ?? string.Empty;
            json[@"number"] = SkinNumber ?? string.Empty;
            SkinNumberFromId = false;

            if (Priority.HasValue && !SettingsHolder.Content.SkinsSkipPriority) {
                json[@"priority"] = Priority.Value;
            } else {
                json.Remove(@"priority");
            }
        }

        protected override void SaveCountry(JObject json) {
            json[@"country"] = Country ?? string.Empty;
        }

        public override void PastLoad() {
            // base.PastLoad();
            // We don’t need to add country and author to suggestion lists: one
            // might be very invalid and other is missing here anyway.

            if (!Enabled || !_fullDetailsRequired) return;

            SuggestionLists.CarSkinTeamsList.AddUnique(Team);
            SuggestionLists.CarSkinDriverNamesList.AddUnique(DriverName);
        }

        protected override void LoadData(JObject json) {
            Name = json.GetStringValueOnly("skinname");
            LoadCountry(json);
            LoadSkinRelated(json);

            if (string.IsNullOrWhiteSpace(Name)) {
                // More than usual case
                // AddError(AcErrorType.Data_ObjectNameIsMissing);
            }

            // LoadTags(json);
            // LoadDescription(json);
            // LoadYear(json);
            // LoadVersionInfo(json);
        }

        public override void SaveData(JObject json) {
            json[@"skinname"] = Name ?? string.Empty;
            SaveCountry(json);
            SaveSkinRelated(json);

            json.Remove(@"tags");
            json.Remove(@"description");
            json.Remove(@"author");
            json.Remove(@"version");
            json.Remove(@"url");

            // SaveTags(json);
            // SaveDescription(json);
            // SaveYear(json);
            // SaveVersionInfo(json);
        }

        protected override KunosDlcInformation GetDlc() {
            return null;
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarSkinTagsList;
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return CarSkinComparer.Comparer.Compare(this, o);
        }

        #region Packing
        public class CarSkinPackerParams : AcCommonObjectPackerParams {
            public bool CmForFlag { get; set; } = true;
            public bool CmPaintShopValues { get; set; } = true;
            public bool PackWithSkinIni { get; set; } = false;
        }

        private class CarSkinPacker : AcCommonObjectPacker<CarSkinObject, CarSkinPackerParams> {
            protected override string GetBasePath(CarSkinObject t) {
                return $"content/cars/{t.CarId}/skins/{t.Id}";
            }

            private static string _recentCarId;
            private static string[] _recentTextures;

            protected override IEnumerable PackOverride(CarSkinObject t) {
                t.FullDetailsRequired();
                yield return Add("preview.jpg", "livery.png", "ui_skin.json");

                if (Params.CmForFlag) {
                    yield return AddString(@"cm_skin_for.json", new JObject {
                        [@"id"] = t.CarId
                    }.ToString(Formatting.Indented));
                }

                if (Params.CmPaintShopValues) {
                    yield return Add("cm_skin.json");
                }

                if (Params.PackWithSkinIni) {
                    var filename = Path.Combine(t.Location, "skin.ini");
                    if (File.Exists(filename)) {
                        yield return Add("skin.ini");

                        IEnumerable AddReferencedTexture(string textureId, string textureType) {
                            if (textureId != null) {
                                textureId = textureId.Trim('\\', '/').Replace(@"\", @"/");
                                if (DataProvider.Instance.KunosCarSkinSets.TryGetValue(textureType, out var set)) {
                                    if (set.Contains(textureId.ToLowerInvariant())) {
                                        yield break;
                                    }
                                }
                                var dir = Path.Combine(AcRootDirectory.Instance.RequireValue, @"content", @"texture", textureType, textureId);
                                if (Directory.Exists(dir)) {
                                    foreach (var texture in Directory.GetFiles(dir, "*.*")) {
                                        yield return AddFile($@"/content/texture/{textureType}/{textureId}/{Path.GetFileName(texture)}", texture);
                                    }
                                }
                            }
                        }

                        var suit = new IniFile(filename);
                        yield return AddReferencedTexture(suit["CREW"].GetNonEmpty("SUIT"), @"crew_suit");
                        yield return AddReferencedTexture(suit["CREW"].GetNonEmpty("HELMET"), @"crew_helmet");
                        yield return AddReferencedTexture(suit["CREW"].GetNonEmpty("BRAND"), @"crew_brand");

                        foreach (var i in suit.Where(x => x.Key != @"CREW")) {
                            yield return AddReferencedTexture(i.Value.GetNonEmpty("SUIT"), @"driver_suit");
                            yield return AddReferencedTexture(i.Value.GetNonEmpty("GLOVES"), @"driver_gloves");
                            yield return AddReferencedTexture(i.Value.GetNonEmpty("HELMET"), @"driver_helmet");
                        }
                    }
                }

                yield return Add("ext_config.ini");

                if (_recentCarId != t.CarId) {
                    _recentCarId = t.CarId;
                    _recentTextures = null;
                    var car = CarsManager.Instance.GetById(t.CarId);
                    if (car != null) {
                        try {
                            _recentTextures = Kn5.FromFile(AcPaths.GetMainCarFilename(car.Location, car.AcdData, false) ?? throw new Exception(),
                                    SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance).TexturesData.Keys.ToArray();
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                }

                if (_recentTextures == null) {
                    yield return Add("*.dds", "*.png", "*.jpg", "*.jpeg", "*.gif");
                } else {
                    yield return Add(_recentTextures);
                }
            }

            protected override PackedDescription GetDescriptionOverride(CarSkinObject t) {
                return new PackedDescription(t.Id, t.Name,
                        new Dictionary<string, string> {
                            ["Made for"] = CarsManager.Instance.GetById(t.CarId)?.DisplayName,
                            ["Version"] = t.Version,
                            ["Made by"] = t.Author,
                            ["Webpage"] = t.Url,
                        }, CarsManager.Instance.Directories.GetMainDirectory(), true) {
                            FolderToMove = t.CarId
                        };
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new CarSkinPacker();
        }
        #endregion

        public const string DraggableFormat = "Data-CarSkinObject";
        string IDraggable.DraggableFormat => DraggableFormat;
    }
}