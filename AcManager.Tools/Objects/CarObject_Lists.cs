using System.ComponentModel;
using AcManager.Tools.Managers;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Objects {

    public partial class CarObject {
        private AcWrapperCollectionView _skinsEnabledWrappersListView;
        public AcWrapperCollectionView SkinsEnabledWrappersList {
            get {
                if (_skinsEnabledWrappersListView != null) return _skinsEnabledWrappersListView;

                _skinsEnabledWrappersListView = new AcWrapperCollectionView(SkinsManager.WrappersAsIList) {
                    Filter = o => (o as AcItemWrapper)?.Value.Enabled == true
                };
                _skinsEnabledWrappersListView.MoveCurrentTo(SelectedSkin);
                _skinsEnabledWrappersListView.CurrentChanged += (sender, args) => {
                    SelectedSkin = (_skinsEnabledWrappersListView.CurrentItem as AcItemWrapper)?.Loaded() as CarSkinObject;
                };
                return _skinsEnabledWrappersListView;
            }
        }

        private BetterListCollectionView _skinsActualListView;
        public BetterListCollectionView SkinsActualList {
            get {
                if (_skinsActualListView != null) return _skinsActualListView;

                _skinsActualListView = new BetterListCollectionView(SkinsManager.LoadedOnlyCollection) {
                    Filter = o => (o as CarSkinObject)?.Enabled == true
                };
                _skinsActualListView.MoveCurrentTo(SelectedSkin);
                _skinsActualListView.CurrentChanged += (sender, args) => {
                    SelectedSkin = _skinsActualListView.CurrentItem as CarSkinObject;
                };
                return _skinsActualListView;
            }
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarTagsList;
        }

        private static ListCollectionView _brandsListView;
        public ListCollectionView BrandsList {
            get {
                if (_brandsListView != null) return _brandsListView;

                _brandsListView = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.CarBrandsList);
                _brandsListView.SortDescriptions.Add(new SortDescription());
                return _brandsListView;
            }
        }

        private static ListCollectionView _carClassesListView;
        public ListCollectionView CarClassesList {
            get {
                if (_carClassesListView != null) return _carClassesListView;

                _carClassesListView = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.CarClassesList);
                _carClassesListView.SortDescriptions.Add(new SortDescription());
                return _carClassesListView;
            }
        }
    }
}
