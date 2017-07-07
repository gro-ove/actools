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
    public class CarSkinsCollectionReadyEventArgs : CollectionReadyEventArgs {
        public readonly string CarId;

        public CarSkinsCollectionReadyEventArgs(string carId, CollectionReadyEventArgs baseArgs) {
            CarId = carId;
            JustReady = baseArgs.JustReady;
        }
    }

    public class CarSkinsManager : AcManagerNew<CarSkinObject> {
        public static event EventHandler<CarSkinsCollectionReadyEventArgs> AnySkinsCollectionReady;
        private readonly EventHandler<CollectionReadyEventArgs> _collectionReadyHandler;

        public string CarId { get; }

        public override IAcDirectories Directories { get; }

        internal CarSkinsManager(string carId, AcDirectoriesBase directories, EventHandler<CollectionReadyEventArgs> collectionReadyHandler) {
            _collectionReadyHandler = collectionReadyHandler;
            CarId = carId;
            Directories = directories;
            InnerWrappersList.CollectionReady += OnCollectionReady;
        }

        private void OnCollectionReady(object sender, CollectionReadyEventArgs e) {
            _collectionReadyHandler.Invoke(sender, e);
            AnySkinsCollectionReady?.Invoke(sender, new CarSkinsCollectionReadyEventArgs(CarId, e));
        }

        private class NumericSortedAcWrapperObservableCollection : SortedAcWrapperObservableCollection {
            protected override int Compare(string x, string y) {
                return AlphanumComparatorFast.Compare(x, y);
            }
        }

        /// <summary>
        /// Gets first enabled skin.
        /// </summary>
        public override CarSkinObject GetDefault() {
            var wrapper = WrappersList.FirstOrDefault(x => x.Value.Enabled);
            return wrapper == null ? null : EnsureWrapperLoaded(wrapper);
        }

        protected override AcWrapperObservableCollection CreateCollection() {
            return new NumericSortedAcWrapperObservableCollection();
        }

        public event AcObjectEventHandler<CarSkinObject> Created;

        protected override CarSkinObject CreateAcObject(string id, bool enabled) {
            return new CarSkinObject(CarId, this, id, enabled);
        }

        protected override CarSkinObject CreateAndLoadAcObject(string id, bool enabled, bool withPastLoad = true) {
            var result = CreateAcObject(id, enabled);
            result.Load();
            if (withPastLoad) {
                Created?.Invoke(this, new AcObjectEventArgs<CarSkinObject>(result));
                result.PastLoad();
            }

            return result;
        }

        protected override async Task LoadAsync() {
            LoadingReset = false;
            await WrappersList.Where(x => !x.IsLoaded).Select(async x => {
                var loaded = await Task.Run(() => CreateAndLoadAcObject(x.Value.Id, x.Value.Enabled, false));
                if (x.IsLoaded) return;

                Created?.Invoke(this, new AcObjectEventArgs<CarSkinObject>(loaded));

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