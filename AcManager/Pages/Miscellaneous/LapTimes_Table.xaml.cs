using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Profile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    public partial class LapTimes_Table : ILoadableContent, IParametrizedUriContent {
        private string _filter;
        private string _key;
        
        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
            _key = _filter;
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await CarsManager.Instance.EnsureLoadedAsync();
            await TracksManager.Instance.EnsureLoadedAsync();
            await LapTimesManager.Instance.UpdateAsync();
        }

        public void Load() {
            CarsManager.Instance.EnsureLoaded();
            TracksManager.Instance.EnsureLoaded();
            LapTimesManager.Instance.UpdateAsync().Forget();
        }

        public void Initialize() {
            InitializeComponent();

            var savedSortPath = LimitedStorage.Get(LimitedSpace.LapTimesSortingColumn, _filter);
            var sortDirection = LimitedStorage.Get(LimitedSpace.LapTimesSortingDescending, _filter) == @"1"
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
            var sortedColumn = Grid.Columns.FirstOrDefault(x => x.SortMemberPath == savedSortPath) ?? DefaultColumn;
            sortedColumn.SortDirection = sortDirection;

            DataContext = new ViewModel(string.IsNullOrEmpty(_filter) ? null : Filter.Create(LapTimeTester.Instance, _filter),
                    sortedColumn.SortMemberPath, sortDirection);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly IFilter<LapTimeWrapped> _filter;

            public ViewModel(IFilter<LapTimeWrapped> filter, string sortPath, ListSortDirection sortDirection) {
                _filter = filter;
                List = WrappedCollection.Create(LapTimesManager.Instance.Entries, x => new LapTimeWrapped(x));
                View = new ListCollectionView((IList)List);

                using (View.DeferRefresh()) {
                    if (_filter == null) {
                        View.Filter = null;
                    } else {
                        View.Filter = FilterTest;
                    }

                    View.SortDescriptions.Add(new SortDescription(sortPath, sortDirection));
                }
            }

            private bool FilterTest(object obj) {
                var t = obj as LapTimeWrapped;
                return t != null && _filter.Test(t);
            }

            public IReadOnlyList<LapTimeWrapped> List { get; }

            public ListCollectionView View { get; }
        }

        private async void OnGridSorting(object sender, DataGridSortingEventArgs e) {
            await Task.Delay(1);
            LimitedStorage.Set(LimitedSpace.LapTimesSortingColumn, _key, e.Column.SortMemberPath);
            LimitedStorage.Set(LimitedSpace.LapTimesSortingDescending, _key, e.Column.SortDirection == ListSortDirection.Descending ? @"1" : @"0");
        }

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.OnlineQuickFilter, e.OldValue, e.NewValue);
            LimitedStorage.Move(LimitedSpace.OnlineSelected, e.OldValue, e.NewValue);
            LimitedStorage.Move(LimitedSpace.OnlineSorting, e.OldValue, e.NewValue);
        }
    }
}
