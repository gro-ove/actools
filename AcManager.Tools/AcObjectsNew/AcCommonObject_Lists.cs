using System.ComponentModel;
using System.Windows.Data;
using AcManager.Tools.Managers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private static ListCollectionView _authorsListView;

        public ListCollectionView AuthorsList {
            get {
                if (_authorsListView != null) return _authorsListView;

                _authorsListView = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.AuthorsList);
                _authorsListView.SortDescriptions.Add(new SortDescription());
                return _authorsListView;
            }
        }

        public string PreviousId { get; internal set; }
    }
}
