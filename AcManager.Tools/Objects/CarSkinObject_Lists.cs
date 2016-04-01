using System.ComponentModel;
using AcManager.Tools.Managers;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Objects {

    public partial class CarSkinObject {
        private static ListCollectionView _teamsListView;
        public ListCollectionView TeamsList {
            get {
                if (_teamsListView != null) return _teamsListView;

                _teamsListView = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.CarSkinTeamsList);
                _teamsListView.SortDescriptions.Add(new SortDescription());
                return _teamsListView;
            }
        }
    }
}
