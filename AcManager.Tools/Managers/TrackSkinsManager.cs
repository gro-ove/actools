using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers {
    public class TrackSkinsCollectionReadyEventArgs : CollectionReadyEventArgs {
        public readonly string TrackId;

        public TrackSkinsCollectionReadyEventArgs(string trackId, CollectionReadyEventArgs baseArgs) {
            TrackId = trackId;
            JustReady = baseArgs.JustReady;
        }
    }

    public class TrackSkinsManager : AcManagerNew<TrackSkinObject>, ICreatingManager {
        public static event EventHandler<TrackSkinsCollectionReadyEventArgs> AnySkinsCollectionReady;
        private readonly EventHandler<CollectionReadyEventArgs> _collectionReadyHandler;

        public string TrackId { get; }

        public override IAcDirectories Directories { get; }

        internal TrackSkinsManager(string trackId, AcDirectoriesBase directories, EventHandler<CollectionReadyEventArgs> collectionReadyHandler) {
            _collectionReadyHandler = collectionReadyHandler;
            TrackId = trackId;
            Directories = directories;
            InnerWrappersList.CollectionReady += OnCollectionReady;
        }

        private void OnCollectionReady(object sender, CollectionReadyEventArgs e) {
            _collectionReadyHandler.Invoke(sender, e);
            AnySkinsCollectionReady?.Invoke(sender, new TrackSkinsCollectionReadyEventArgs(TrackId, e));
        }

        private class NumericSortedAcWrapperObservableCollection : SortedAcWrapperObservableCollection {
            protected override int Compare(string x, string y) {
                return AlphanumComparatorFast.Compare(x, y);
            }
        }

        /// <summary>
        /// Gets first enabled skin.
        /// </summary>
        public override TrackSkinObject GetDefault() {
            var wrapper = WrappersList.FirstOrDefault(x => x.Value.Enabled);
            return wrapper == null ? null : EnsureWrapperLoaded(wrapper);
        }

        protected override AcWrapperObservableCollection CreateCollection() {
            return new NumericSortedAcWrapperObservableCollection();
        }

        public event AcObjectEventHandler<TrackSkinObject> Created;

        protected override TrackSkinObject CreateAcObject(string id, bool enabled) {
            return new TrackSkinObject(TrackId, this, id, enabled);
        }

        protected override TrackSkinObject CreateAndLoadAcObject(string id, bool enabled, bool withPastLoad = true) {
            var result = CreateAcObject(id, enabled);
            result.Load();
            if (withPastLoad) {
                Created?.Invoke(this, new AcObjectEventArgs<TrackSkinObject>(result));
                result.PastLoad();
            }

            return result;
        }

        protected override async Task LoadAsync() {
            LoadingReset = false;
            await WrappersList.Where(x => !x.IsLoaded).Select(async x => {
                var loaded = await Task.Run(() => CreateAndLoadAcObject(x.Value.Id, x.Value.Enabled, false));
                if (x.IsLoaded) return;

                Created?.Invoke(this, new AcObjectEventArgs<TrackSkinObject>(loaded));

                x.Value = loaded;
                loaded.PastLoad();
            }).WhenAll(SettingsHolder.Content.LoadingConcurrency);

            IsLoaded = true;
            ListReady();

            if (LoadingReset) {
                Load();
            }
        }

        protected override void LogLoadingTime(TimeSpan s) {}

        #region Update ID in JSON-file
        private static void FixId(string location, string id) {
            var file = Path.Combine(location, "ui_track_skin.json");
            if (!File.Exists(file)) return;

            try {
                var json = JsonExtension.Parse(File.ReadAllText(file));
                json["id"] = id;
                File.WriteAllText(file, json.ToString(Formatting.Indented));
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        protected override async Task MoveOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            AssertId(newId);

            await Task.Run(() => {
                FileUtils.Move(oldLocation, newLocation);
                foreach (var tuple in attachedOldNew.Where(x => FileUtils.Exists(x.Item1))) {
                    FileUtils.Move(tuple.Item1, tuple.Item2);
                }

                FixId(newLocation, newId);
            });

            var obj = CreateAndLoadAcObject(newId, newEnabled);
            obj.PreviousId = oldId;
            ReplaceInList(oldId, new AcItemWrapper(this, obj));
        }

        protected override async Task CloneOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            AssertId(newId);

            await Task.Run(() => {
                FileUtils.Copy(oldLocation, newLocation);
                foreach (var tuple in attachedOldNew.Where(x => FileUtils.Exists(x.Item1))) {
                    FileUtils.Copy(tuple.Item1, tuple.Item2);
                }

                FixId(newLocation, newId);
            });

            AddInList(new AcItemWrapper(this, CreateAndLoadAcObject(newId, newEnabled)));
        }
        #endregion

        public IAcObjectNew AddNew(string id = null) {
            if (Directories == null) return null;
            var mainDirectory = Directories.GetMainDirectory();

            if (id == null) {
                var uniqueId = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(mainDirectory, "skin")));
                id = Prompt.Show("Choose a name for a new track skin:", "New track skin", required: true, maxLength: 80, placeholder: "?",
                        defaultValue: uniqueId);
                if (id == null) return null;
            }

            var directory = Directories.GetLocation(id, true);
            if (Directory.Exists(directory)) {
                throw new InformativeException("Can’t add a new object", $"ID “{id}” is already taken.");
            }

            using (IgnoreChanges()) {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "ui_track_skin.json"), new JObject {
                    ["name"] = AcStringValues.NameFromId(id),
                    ["track"] = TrackId,
                    ["id"] = id,
                }.ToString(Formatting.Indented));

                var obj = CreateAndLoadAcObject(id, true);
                InnerWrappersList.Add(new AcItemWrapper(this, obj));
                UpdateList(true);

                return obj;
            }
        }
    }
}