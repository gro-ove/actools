using System;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class TrackSkinsCollectionReadyEventArgs : CollectionReadyEventArgs {
        public readonly string TrackId;

        public TrackSkinsCollectionReadyEventArgs(string carId, CollectionReadyEventArgs baseArgs) {
            TrackId = carId;
            JustReady = baseArgs.JustReady;
        }
    }

    public class TTrackSkinsManager : AcManagerNew<TrackSkinObject> {
        public static event EventHandler<TrackSkinsCollectionReadyEventArgs> AnySkinsCollectionReady;
        private readonly EventHandler<CollectionReadyEventArgs> _collectionReadyHandler;

        public string TrackId { get; }

        public override IAcDirectories Directories { get; }

        internal TrackSkinsManager(string carId, AcDirectoriesBase directories, EventHandler<CollectionReadyEventArgs> collectionReadyHandler) {
            _collectionReadyHandler = collectionReadyHandler;
            TrackId = carId;
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
    }
}