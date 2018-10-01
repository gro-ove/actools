using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    [MoonSharpUserData]
    public abstract partial class TrackObjectBase : AcJsonObjectNew, IDraggable {
        protected TrackObjectBase(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) { }

        public override void PastLoad() {
            base.PastLoad();

            if (Enabled) {
                SuggestionLists.CitiesList.AddUnique(City);
            }
        }

        protected override void ClearData() {
            base.ClearData();
            City = null;
            GeoTags = null;
            SpecsLength = null;
            SpecsPitboxes = null;
            SpecsWidth = null;
        }

        public override void Reload() {
            OnImageChangedValue(PreviewImage);
            OnImageChangedValue(OutlineImage);
            base.Reload();
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;

            if (FileUtils.IsAffectedBy(PreviewImage, filename)) {
                OnImageChangedValue(PreviewImage);
                CheckPreview();
            } else if (FileUtils.IsAffectedBy(OutlineImage, filename)) {
                OnImageChangedValue(OutlineImage);
                CheckOutline();
            } else if (FileUtils.IsAffectedBy(MapImage, filename)) {
                OnImageChangedValue(MapImage);
                CheckMap();
            } else if (FileUtils.IsAffectedBy(AiLaneFastFilename, filename)) {
                _aiLaneFastExists = null;
                OnPropertyChanged(nameof(AiLaneFastExists));
            } else if (FileUtils.IsAffectedBy(AiLaneFastCandidateFilename, filename)) {
                _aiLaneFastCandidateExists = null;
                OnPropertyChanged(nameof(AiLaneFastCandidateExists));
                OnPropertyChanged(nameof(AiLaneCandidateExists));
                _applyAiLaneCandidatesCommand?.RaiseCanExecuteChanged();
            } else if (FileUtils.IsAffectedBy(AiLanePitCandidateFilename, filename)) {
                _aiLanePitCandidateExists = null;
                OnPropertyChanged(nameof(AiLanePitCandidateExists));
                OnPropertyChanged(nameof(AiLaneCandidateExists));
                _applyAiLaneCandidatesCommand?.RaiseCanExecuteChanged();
            }

            return true;
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckMap();
            CheckOutline();
            CheckPreview();
        }

        private void CheckMap() {
            ErrorIf(!File.Exists(MapImage), AcErrorType.Track_MapIsMissing);
        }

        private void CheckOutline() {
            ErrorIf(!File.Exists(OutlineImage), AcErrorType.Track_OutlineIsMissing);
        }

        private void CheckPreview() {
            ErrorIf(!File.Exists(PreviewImage), AcErrorType.Track_PreviewIsMissing);
        }

        [NotNull]
        public abstract TrackObject MainTrackObject { get; }

        // Tricky part: some JSON files might be already loaded and parsed while trying to figure out which layout is
        // more appropriate to use as a default one.
        protected static readonly Dictionary<string, JObject> RecentlyParsed = new Dictionary<string, JObject>();

        protected override bool LoadJsonOrThrow() {
            if (!RecentlyParsed.TryGetValue(JsonFilename, out var jObject)) {
                return base.LoadJsonOrThrow();
            }

            RecentlyParsed.Remove(JsonFilename);
            JsonObject = jObject;
            LoadData(jObject);
            return true;
        }

        [CanBeNull]
        public abstract string LayoutId { get; }

        public abstract string IdWithLayout { get; }

        public string KunosIdWithLayout => IdWithLayout.Replace('/', '-');

        public abstract string LayoutName { get; set; }

        #region Properties & Specifications
        private string _city;

        [CanBeNull]
        public string City {
            get => _city;
            set {
                if (value == _city) return;
                _city = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(City));
                    Changed = true;

                    SuggestionLists.RebuildCitiesList();
                }
            }
        }

        private GeoTagsEntry _geoTags;

        [CanBeNull]
        public GeoTagsEntry GeoTags {
            get => _geoTags;
            set {
                if (value == _geoTags) return;
                _geoTags = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(GeoTags));
                    Changed = true;
                }
            }
        }

        private string _specsLength;
        private string _specsLengthDisplay;

        [CanBeNull]
        public string SpecsLength {
            get => _specsLength;
            set {
                value = value?.Trim();
                if (value == _specsLength) return;
                _specsLength = value;
                _specsLengthValue = null;
                _specsLengthDisplay = null;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsLength));
                    OnPropertyChanged(nameof(SpecsLengthValue));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsWidth;
        private string _specsWidthDisplay;

        [CanBeNull]
        public string SpecsWidth {
            get => _specsWidth;
            set {
                value = value?.Trim();
                if (value == _specsWidth) return;
                _specsWidth = value;
                _specsWidthDisplay = null;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsWidth));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private string _specsPitboxes;
        private string _specsPitboxesDisplay;

        [CanBeNull]
        public string SpecsPitboxes {
            get => _specsPitboxes;
            set {
                value = value?.Trim();
                if (value == _specsPitboxes) return;
                _specsPitboxes = value;
                _specsPitboxesValue = null;
                _specsPitboxesDisplay = null;

                if (Loaded) {
                    OnPropertyChanged(nameof(SpecsPitboxes));
                    OnPropertyChanged(nameof(SpecsPitboxesValue));
                    OnPropertyChanged(nameof(SpecsInfoDisplay));
                    Changed = true;
                }
            }
        }

        private static readonly Regex SpecsLengthFix = new Regex(@"^\W*(\d+(?:[\.,]\d+)?)\W*(?:(km|kilometers)|(mi|miles?)|m)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex SpecsDotFixTest = new Regex(@"\d[\.,]\d{3}(?=\D|$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private double? _specsLengthValue;

        /// <summary>
        /// Parsed value, in meters.
        /// </summary>
        public double SpecsLengthValue => _specsLengthValue ?? (_specsLengthValue = GetSpecsLengthValue(_specsLength, true) ?? 0d).Value;

        private int? _specsPitboxesValue;

        /// <summary>
        /// Parsed value.
        /// </summary>
        public int SpecsPitboxesValue => _specsPitboxesValue ?? (_specsPitboxesValue = FlexibleParser.TryParseInt(SpecsPitboxes) ?? 2).Value;

        private double? GetSpecsLengthValue([CanBeNull] string original, bool dotFix) {
            if (original == null) return null;

            if (!double.TryParse(original, NumberStyles.Float | NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
                var m = SpecsLengthFix.Match(original);

                if (m.Success) {
                    var n = m.Groups[1].Value;
                    if (n.IndexOf(',') != -1) {
                        n = n.Replace(',', '.');
                    }

                    if (!FlexibleParser.TryParseDouble(n, out value)) {
                        return null;
                    }

                    if (m.Groups[2].Success) {
                        value *= 1e3;
                    } else if (m.Groups[3].Success) {
                        value *= 1.6e3;
                    }
                } else if (!FlexibleParser.TryParseDouble(original, out value)) {
                    return null;
                }
            }

            if (dotFix && value < 100 && SpecsDotFixTest.IsMatch(original)) {
                // For those lost and misguided souls who use “.” as a thousands separator or “,” as decimal separators.
                value *= 1e3;
            }

            return value;
        }

        private string D(string a, string b) {
#if DEBUG
            return $@"{a} 〈{b}〉";
#else
            return a;
#endif
        }

        [CanBeNull]
        private string GetSpecsLengthDisplay() {
            var value = SpecsLengthValue;
            if (Equals(value, 0d)) {
                return SpecsLength;
            }

            return D((value / 1000).Round(0.1) * SettingsHolder.CommonSettings.DistanceMultiplier + SettingsHolder.CommonSettings.SpaceDistancePostfix, SpecsLength);
        }

        private static readonly Regex SpecsWidthFix = new Regex(@"^(.+?)\s*[-–—]\s*(.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private string GetSpecsWidthDisplayPart(string original) {
            var value = GetSpecsLengthValue(original, false);
            if (!value.HasValue) return original;
            return value.Value.Round(0.1) * SettingsHolder.CommonSettings.ShortDistanceMultiplier + SettingsHolder.CommonSettings.SpaceShortDistancePostfix;
        }

        [CanBeNull]
        private string GetSpecsWidthDisplay() {
            var original = SpecsWidth;
            if (original == null) return null;

            var m = SpecsWidthFix.Match(original);
            if (m.Success) {
                return D(GetSpecsWidthDisplayPart(m.Groups[1].Value) + @"–" + GetSpecsWidthDisplayPart(m.Groups[2].Value), original);
            }

            var result = D(GetSpecsWidthDisplayPart(original), original);
            if (result != null) {
                return result;
            }

            if (double.TryParse(original, NumberStyles.Float | NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
                return value * SettingsHolder.CommonSettings.ShortDistanceMultiplier + SettingsHolder.CommonSettings.SpaceShortDistancePostfix;
            }

            return original;
        }

        [CanBeNull]
        private string GetSpecsPitboxesDisplay() {
            return PluralizingConverter.PluralizeExt(SpecsPitboxesValue, ToolsStrings.TrackBaseObject_Specs_PitsNumber);
        }

        [NotNull]
        public string SpecsInfoDisplay => new[] {
            string.IsNullOrEmpty(SpecsLength) ? null : (_specsLengthDisplay ?? (_specsLengthDisplay = GetSpecsLengthDisplay())),
            string.IsNullOrEmpty(SpecsWidth) ? null : (_specsWidthDisplay ?? (_specsWidthDisplay = GetSpecsWidthDisplay())),
            string.IsNullOrEmpty(SpecsPitboxes) ? null : (_specsPitboxesDisplay ?? (_specsPitboxesDisplay = GetSpecsPitboxesDisplay()))
        }.NonNull().JoinToString(@", ");
        #endregion

        public TimeSpan GuessApproximateLapDuration(CarObject car = null) {
            var averageSpeed = ((FlexibleParser.TryParseDouble(car?.SpecsTopSpeed) ?? 200d) * 0.3).Clamp(20d, 200d);
            return TimeSpan.FromHours(SpecsLengthValue / 1e3 / averageSpeed).Clamp(TimeSpan.FromSeconds(30), TimeSpan.FromHours(2));
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.TrackTagsList;
        }

        protected override void LoadData(JObject json) {
            base.LoadData(json);

            if (Version == null && Description != null) {
                Version = AcStringValues.GetVersionFromName(Description, out var description);
                if (Version != null) {
                    Description = description;
                }
            }

            City = json.GetStringValueOnly("city");
            GeoTags = json.GetGeoTagsValueOnly("geotags");

            if (Country == null) {
                foreach (var country in Tags.Select(AcStringValues.CountryFromTag).Where(x => x != null)) {
                    Country = country;
                    break;
                }
            }

            SpecsLength = json.GetStringValueOnly("length");
            SpecsWidth = json.GetStringValueOnly("width");
            SpecsPitboxes = json.GetStringValueOnly("pitboxes");
        }

        protected override void LoadYear(JObject json) {
            Year = json.GetIntValueOnly("year");
            if (Year.HasValue) return;

            if (DataProvider.Instance.TrackYears.TryGetValue(Id, out var year)) {
                Year = year;
            } else if (Name != null) {
                Year = AcStringValues.GetYearFromName(Name) ?? AcStringValues.GetYearFromId(Name);
            }
        }

        protected override bool TestIfKunos() {
            return LayoutId != null ?
                    (DataProvider.Instance.GetKunosContentIds(@"layouts")?.ArrayContains(IdWithLayout) ?? false) :
                    (DataProvider.Instance.GetKunosContentIds(@"tracks")?.ArrayContains(Id) ?? false);
        }

        public override void SaveData(JObject json) {
            base.SaveData(json);

            json[@"city"] = City;

            if (GeoTags != null) {
                json[@"geotags"] = GeoTags.ToJObject();
            } else {
                json.Remove(@"geotags");
            }

            json[@"length"] = SpecsLength;
            json[@"width"] = SpecsWidth;
            json[@"pitboxes"] = SpecsPitboxes;
        }

        public string PreviewImage { get; protected set; }
        public string OutlineImage { get; protected set; }

        public abstract string LayoutDataDirectory { get; }

        public string DataDirectory => Path.Combine(LayoutDataDirectory, @"data");
        public string MapImage => Path.Combine(LayoutDataDirectory, @"map.png");

        private string _aiLaneFastFilename;
        public string AiLaneFastFilename => _aiLaneFastFilename ?? (_aiLaneFastFilename = Path.Combine(LayoutDataDirectory, @"ai", @"fast_lane.ai"));

        private string _aiLanePitFilename;
        public string AiLanePitFilename => _aiLanePitFilename ?? (_aiLanePitFilename = Path.Combine(LayoutDataDirectory, @"ai", @"pit_lane.ai"));

        private string _aiLaneFastCandidateFilename;

        public string AiLaneFastCandidateFilename
            => _aiLaneFastCandidateFilename ?? (_aiLaneFastCandidateFilename = Path.Combine(LayoutDataDirectory, @"ai", @"fast_lane.ai.candidate"));

        private string _aiLanePitCandidateFilename;

        public string AiLanePitCandidateFilename
            => _aiLanePitCandidateFilename ?? (_aiLanePitCandidateFilename = Path.Combine(LayoutDataDirectory, @"ai", @"pit_lane.ai.candidate"));

        private bool? _aiLaneFastExists;
        public bool AiLaneFastExists => _aiLaneFastExists ?? (_aiLaneFastExists = File.Exists(AiLaneFastFilename)).Value;

        private bool? _aiLaneFastCandidateExists;
        public bool AiLaneFastCandidateExists => _aiLaneFastCandidateExists ?? (_aiLaneFastCandidateExists = File.Exists(AiLaneFastCandidateFilename)).Value;

        private bool? _aiLanePitCandidateExists;
        public bool AiLanePitCandidateExists => _aiLanePitCandidateExists ?? (_aiLanePitCandidateExists = File.Exists(AiLanePitCandidateFilename)).Value;

        public bool AiLaneCandidateExists => AiLanePitCandidateExists || AiLaneFastCandidateExists;

        private DelegateCommand _applyAiLaneCandidatesCommand;

        public DelegateCommand ApplyAiLaneCandidatesCommand => _applyAiLaneCandidatesCommand ?? (_applyAiLaneCandidatesCommand = new DelegateCommand(() => {
            if (AiLaneFastCandidateExists) {
                FileUtils.Recycle(AiLaneFastFilename);
                File.Move(AiLaneFastCandidateFilename, AiLaneFastFilename);
            }

            if (AiLanePitCandidateExists) {
                FileUtils.Recycle(AiLanePitFilename);
                File.Move(AiLanePitCandidateFilename, AiLanePitFilename);
            }
        }, () => AiLaneCandidateExists));

        public string ModelsFilename => Path.Combine(MainTrackObject.Location, LayoutId == null ? @"models.ini" : $@"models_{LayoutId}.ini");

        #region Draggable
        public const string DraggableFormat = "Data-TrackObject";

        string IDraggable.DraggableFormat => DraggableFormat;
        #endregion

        protected override AcCommonObjectPacker CreatePacker() {
            throw new InformativeException("Can’t pack track", "At the moment, tracks packing is not implemented.");
        }
    }
}
