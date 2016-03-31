using System.ComponentModel;
using AcManager.Tools.Managers;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Objects {

    public partial class CarObject {
        private AcWrapperCollectionView _skinsListView;
        public AcWrapperCollectionView SkinsList {
            get {
                if (_skinsListView != null) return _skinsListView;

                _skinsListView = new AcWrapperCollectionView(SkinsManager.WrappersAsIList) {
                    Filter = o => (o as AcItemWrapper)?.Value.Enabled == true
                };
                _skinsListView.MoveCurrentTo(SelectedSkin);
                _skinsListView.CurrentChanged += (sender, args) => {
                    SelectedSkin = (_skinsListView.CurrentItem as AcItemWrapper)?.Loaded() as CarSkinObject;
                };
                return _skinsListView;
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
