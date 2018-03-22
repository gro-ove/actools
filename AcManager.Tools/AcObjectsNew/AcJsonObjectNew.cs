using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.AcObjectsNew {
    public interface IAcObjectVersionInformation {
        [CanBeNull]
        string Version { get; }
    }

    public interface IAcObjectAuthorInformation {
        [CanBeNull]
        string Author { get; }
    }

    public interface IAcObjectFullAuthorshipInformation : IAcObjectVersionInformation, IAcObjectAuthorInformation {
        [CanBeNull]
        string Url { get; set; }

        [CanBeNull]
        string VersionInfoDisplay { get; }
    }

    public static class AcObjectAuthorInformationExtension {
        public static string GetVersionInfoDisplay([NotNull] this IAcObjectFullAuthorshipInformation obj) {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            if (obj.Version == null && obj.Author == null) {
                return obj.Url != null ? $@"[url={BbCodeBlock.EncodeAttribute(obj.Url)}]{BbCodeBlock.Encode(obj.Url)}[/url]" : null;
            }

            var result = obj.Author == null ? obj.Version
                    : obj.Version == null ? obj.Author : $"{obj.Author} ({obj.Version})";
            return obj.Url != null ? $@"[url={BbCodeBlock.EncodeAttribute(obj.Url)}]{BbCodeBlock.Encode(result)}[/url]" : result;
        }
    }

    public abstract partial class AcJsonObjectNew : AcCommonObject, IAcObjectFullAuthorshipInformation {
        protected AcJsonObjectNew(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            Tags = new TagsCollection();
        }

        public override void PastLoad() {
            SuggestionLists.AuthorsList.AddUnique(Author);
            if (!Enabled) return;

            GetTagsList().AddUniques(Tags);
            SuggestionLists.CountriesList.AddUnique(Country);
        }

        public void ReloadJsonData() {
            ClearErrors(AcErrorCategory.Data);
            if (!LoadJsonOrThrow()) {
                ClearData();
            }
            Changed = false;
        }

        public override bool HandleChangedFile(string filename) {
            if (!FileUtils.Affects(filename, JsonFilename)) return false;

            if (!Changed || ModernDialog.ShowMessage(ToolsStrings.AcObject_ReloadAutomatically_Json, ToolsStrings.AcObject_ReloadAutomatically,
                    MessageBoxButton.YesNo, "autoReload") == MessageBoxResult.Yes) {
                ReloadJsonData();
            }

            return true;
        }

        public string JsonFilename { get; protected set; }

        private JObject _jsonObject;

        public JObject JsonObject {
            get => _jsonObject;
            set {
                if (_jsonObject == value) return;

                _jsonObject = value;
                OnPropertyChanged(nameof(HasData));
            }
        }

        public override bool HasData => _jsonObject != null;

        #region Loading and saving
        protected override void LoadOrThrow() {
            if (!LoadJsonOrThrow()) {
                ClearData();
            }
        }

        protected virtual bool LoadJsonOrThrow() {
            string text;

            try {
                text = FileUtils.ReadAllText(JsonFilename);
            } catch (FileNotFoundException) {
                AddError(AcErrorType.Data_JsonIsMissing, Path.GetFileName(JsonFilename));
                return false;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Data_JsonIsMissing, Path.GetFileName(JsonFilename));
                return false;
            }

            try {
                JsonObject = JsonExtension.Parse(text);
            } catch (Exception) {
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(JsonFilename));
                return false;
            }

            LoadData(JsonObject);
            return true;
        }

        protected virtual void ClearData() {
            JsonObject = null;

            Tags = new TagsCollection();
            Name = null;
            Year = null;
            Country = null;
            Description = null;
            Country = null;
            Version = null;
            Author = null;
            Url = null;
        }

        protected virtual void LoadData(JObject json) {
            Name = json.GetStringValueOnly("name")?.Trim();
            if (string.IsNullOrEmpty(Name)) {
                AddError(AcErrorType.Data_ObjectNameIsMissing);
            }

            LoadTags(json);
            LoadCountry(json);
            LoadDescription(json);
            LoadYear(json);
            LoadVersionInfo(json);
        }

        protected void LoadTags(JObject json) {
            Tags = json[@"tags"] is JArray tags ? new TagsCollection(tags.Select(x => x.ToString())) : new TagsCollection();
        }

        protected void LoadCountry(JObject json) {
            var value = json.GetStringValueOnly("country")?.Trim();
            Country = value != null ? AcStringValues.CountryFromTag(value) ?? value :
                    Tags.Select(AcStringValues.CountryFromTag).FirstOrDefault(x => x != null);
        }

        protected void LoadDescription(JObject json) {
            Description = AcStringValues.DecodeDescription(json.GetStringValueOnly("description"));
        }

        protected virtual void LoadYear(JObject json) {
            Year = json.GetIntValueOnly("year");
            if (!Year.HasValue && Name != null) {
                Year = AcStringValues.GetYearFromName(Name);
            }
        }

        protected virtual bool TestIfKunos() {
            return Id.StartsWith(@"ks_");
        }

        protected virtual void LoadVersionInfo(JObject json) {
            if (TestIfKunos()) {
                Author = AuthorKunos;
            } else {
                var author = json.GetStringValueOnly("author")?.Trim();
                Author = author == AuthorKunos ? null : author;
            }

            Version = json.GetStringValueOnly("version")?.Trim();
            Url = json.GetStringValueOnly("url")?.Trim();

            if (Version == null && Name != null) {
                Version = AcStringValues.GetVersionFromName(Name, out var name);
                if (Version != null) {
                    Name = name;
                }
            }
        }

        public virtual void SaveData(JObject json) {
            json[@"name"] = Name ?? "";
            SaveTags(json);
            SaveCountry(json);
            SaveDescription(json);
            SaveYear(json);
            SaveVersionInfo(json);
        }

        protected void SaveTags(JObject json) {
            json[@"tags"] = new JArray(Tags.Select(x => (object)x).ToArray());
        }

        protected virtual void SaveCountry(JObject json) {
            json[@"country"] = Country;
        }

        protected void SaveDescription(JObject json) {
            json[@"description"] = AcStringValues.EncodeDescription(Description);
        }

        protected void SaveYear(JObject json) {
            if (Year.HasValue) {
                json[@"year"] = Year.Value;
            } else {
                json.Remove(@"year");
            }
        }

        protected void SaveVersionInfo(JObject json) {
            json[@"author"] = Author;
            json[@"version"] = Version;
            json[@"url"] = Url;
        }

        public override Task SaveAsync() {
            var json = JsonObject;
            if (json != null) {
                SaveData(json);
                using (CarsManager.Instance.IgnoreChanges()) {
                    File.WriteAllText(JsonFilename, json.ToString());
                }
                Changed = false;
            }
            return Task.Delay(0);
        }
        #endregion

        #region Common fields
        private string _country;

        [CanBeNull]
        public string Country {
            get => _country;
            set {
                if (value == _country) return;
                _country = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(Country));
                    Changed = true;
                    SuggestionLists.RebuildCountriesList();
                }
            }
        }

        private string _description;

        [CanBeNull]
        public string Description {
            get => _description;
            set {
                if (value == _description) return;
                _description = value;

                if (Loaded) {
                    OnPropertyChanged(nameof(Description));
                    Changed = true;
                }
            }
        }
        #endregion

        #region Version info
        private string _author;

        public string Author {
            get => _author;
            set {
                if (value == _author) return;
                _author = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Author));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                    SuggestionLists.RebuildAuthorsList();
                }
            }
        }

        private string _version;

        public string Version {
            get => _version;
            set {
                if (value == _version) return;
                _version = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Version));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                }

                OnVersionChanged();
            }
        }

        protected virtual void OnVersionChanged() { }

        private string _url;

        public string Url {
            get => _url;
            set {
                if (value == _url) return;
                _url = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

                if (Loaded) {
                    OnPropertyChanged(nameof(Url));
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                }
            }
        }

        public virtual string VersionInfoDisplay => this.GetVersionInfoDisplay();
        #endregion

        #region DLC-related stuff
        private KunosDlcInformation _dlc;
        private bool _dlcSet;

        [CanBeNull]
        public KunosDlcInformation Dlc {
            get {
                if (!_dlcSet) {
                    _dlcSet = true;
                    if (Author == AuthorKunos) {
                        _dlc = GetDlc();
                    }
                }

                return _dlc;
            }
        }

        [CanBeNull]
        protected abstract KunosDlcInformation GetDlc();
        #endregion
    }
}