using System.ComponentModel;
using AcManager.Tools.Managers;
using System.Windows.Data;

namespace AcManager.Tools.Objects {

    public partial class TrackObjectBase {
        private static ListCollectionView _citiesList;
        public ListCollectionView CitiesList {
            get {
                if (_citiesList != null) return _citiesList;

                _citiesList = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.CitiesList);
                _citiesList.SortDescriptions.Add(new SortDescription());
                return _citiesList;
            }
        }
    }
}
