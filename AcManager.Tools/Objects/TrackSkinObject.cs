using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class TrackSkinObject : AcJsonObjectNew, IDraggable {
        [NotNull]
        public string TrackId { get; }

        public TrackSkinObject([NotNull] string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            TrackId = carId;
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

        public override Task SaveAsync() {
            var json = JsonObject ?? new JObject();
            SaveData(json);

            using (CarsManager.Instance.IgnoreChanges()) {
                File.WriteAllText(JsonFilename, json.ToString());
            }

            Changed = false;
            return Task.Delay(0);
        }

        protected override void ClearData() {
            base.ClearData();
            Priority = 0;
            Categories = new TagsCollection();
        }

        public override void Reload() {
            base.Reload();
            OnImageChangedValue(PreviewImage);
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.Affects(filename, PreviewImage)) {
                OnImageChangedValue(PreviewImage);
                CheckPreview();
                return true;
            }

            return true;
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, "ui_track_skin.json");
            PreviewImage = Path.Combine(Location, "preview.png");
        }

        public string PreviewImage { get; private set; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckPreview();
        }

        private void CheckPreview() {
            /*ErrorIf(!File.Exists(PreviewImage), AcErrorType.TrackSkin_PreviewIsMissing, Id);*/
        }

        protected override void SaveCountry(JObject json) {
            json[@"country"] = Country ?? string.Empty;
        }

        /*public override void PastLoad() {
            base.PastLoad();
            /* we don't need to add country and author to suggestion lists: one
               might be very invalid and other is missing here anyway #1#

            /*if (!Enabled) return;

            SuggestionLists.TrackSkinTeamsList.AddUnique(Team);
            SuggestionLists.TrackSkinDriverNamesList.AddUnique(DriverName);#1#
        }*/

        protected override void LoadData(JObject json) {
            base.LoadData(json);
            Categories = new TagsCollection((json[@"categories"] as JArray)?.Select(x => x.ToString()));
            Priority = json.GetDoubleValueOnly(@"priority", 0d);
        }

        public override void SaveData(JObject json) {
            base.SaveData(json);
            json[@"categories"] = new JArray(Categories);
            json[@"priority"] = Priority;
            json[@"id"] = Id;
            json[@"track"] = TrackId;
        }

        protected override KunosDlcInformation GetDlc() {
            return null;
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.TrackSkinTagsList;
        }

        #region Enabled/disabled
        private bool _isActive;

        public bool IsActive {
            get => _isActive;
            set {
                if (Equals(value, _isActive)) return;
                _isActive = value;
                OnPropertyChanged();
                TracksManager.Instance.GetById(TrackId)?.RefreshSkins(this);
            }
        }
        #endregion

        #region Properties
        private double _priority;

        public double Priority {
            get => _priority;
            set {
                if (Equals(value, _priority)) return;
                _priority = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion

        #region Categories
        public ListCollectionView CategoriesList => SuggestionLists.TrackSkinCategoriesList.View;

        private TagsCollection _categories;

        [NotNull]
        public TagsCollection Categories {
            get => _categories;
            set {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value == _categories || _categories != null && value.SequenceEqual(_categories)) return;

                if (_categories != null) {
                    _categories.CollectionChanged -= OnCategoriesCollectionChanged;
                }

                _categories = value;
                OnPropertyChanged();

                _categories.CollectionChanged += OnCategoriesCollectionChanged;

                if (Loaded) {
                    Changed = true;
                    RebuildTagsList();
                }
            }
        }

        protected void RebuildCategoriesList() {
            /*SuggestionLists.TrackSkinCategoriesList.ReplaceEverythingBy(Manager.OfType<TrackSkinObject>().SelectMany(x => x.Categories));*/
        }

        private void OnCategoriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (Loaded) {
                RebuildCategoriesList();
                Changed = true;
            }
        }
        #endregion

        #region Packing
        public class TrackSkinPackerParams : AcCommonObjectPackerParams {
            public bool JsgmeCompatible { get; set; } = true;
            public string IncludeJsgme { get; set; } = null;
        }

        private class TrackSkinPacker : AcCommonObjectPacker<TrackSkinObject, TrackSkinPackerParams> {
            protected override string GetBasePath(TrackSkinObject t) {
                return Params.JsgmeCompatible ?
                        $"MODS/{FileUtils.EnsureFileNameIsValid(t.DisplayName + " " + t.Version).Trim()}/content/tracks/{t.TrackId}/skins/default" :
                        $"content/tracks/{t.TrackId}/skins/cm_skins/{t.Id}";
            }

            protected override IEnumerable PackOverride(TrackSkinObject t) {
                var j = new JObject();
                t.SaveData(j);
                yield return AddString("ui_track_skin.json", j.ToString(Formatting.Indented));
                yield return Add("preview.png", "*.dds", "*.png", "*.jpeg", "*.jpg");

                if (Params.IncludeJsgme != null && File.Exists(Params.IncludeJsgme)) {
                    AddFilename("/JSGME.exe", Params.IncludeJsgme);
                }
            }

            protected override PackedDescription GetDescriptionOverride(TrackSkinObject t) {
                return new PackedDescription(t.Id, t.Name,
                        new Dictionary<string, string> {
                            ["Made for"] = TracksManager.Instance.GetById(t.TrackId)?.DisplayNameWithoutCount,
                            ["Version"] = t.Version,
                            ["Made by"] = t.Author,
                            ["Webpage"] = t.Url,
                        }, TracksManager.Instance.Directories.GetMainDirectory(), true) {
                            FolderToMove = t.TrackId
                        };
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new TrackSkinPacker();
        }
        #endregion

        public const string DraggableFormat = "Data-TrackSkinObject";
        string IDraggable.DraggableFormat => DraggableFormat;
    }
}