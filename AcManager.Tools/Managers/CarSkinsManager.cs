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
    public class CarSkinsManager : AcManagerNew<CarSkinObject> {
        public readonly string CarId;

        internal CarSkinsManager(string carId, BaseAcDirectories directories) {
            CarId = carId;
            Directories = directories;
        }

        private class NumericSortedAcWrapperObservableCollection : SortedAcWrapperObservableCollection {
            protected override int Compare(string x, string y) {
                return AlphanumComparatorFast.Compare(x, y);
            }
        }

        protected override AcWrapperObservableCollection CreateCollection() {
            return new NumericSortedAcWrapperObservableCollection();
        }

        public override BaseAcDirectories Directories { get; }

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
            await TaskExtension.WhenAll(WrappersList.Where(x => !x.IsLoaded).Select(async x => {
                var loaded = await Task.Run(() => CreateAndLoadAcObject(x.Value.Id, x.Value.Enabled, false));
                if (x.IsLoaded) return;

                Created?.Invoke(this, new AcObjectEventArgs<CarSkinObject>(loaded));

                x.Value = loaded;
                loaded.PastLoad();
            }), SettingsHolder.Content.LoadingConcurrency);

            IsLoaded = true;
            ListReady();

            if (LoadingReset) {
                Load();
            }
        }
    }
}