using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class CarSkinsManager : FileAcManager<CarSkinObject> {
        public readonly string CarId;

        internal CarSkinsManager(string carId, AcObjectTypeDirectories directories) {
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

        public override AcObjectTypeDirectories Directories { get; }

        protected override CarSkinObject CreateAcObject(string id, bool enabled) {
            return new CarSkinObject(CarId, this, id, enabled);
        }
    }
}