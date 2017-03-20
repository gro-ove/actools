using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    public partial class LapTimes_Table : ILoadableContent, IParametrizedUriContent {
        private string _filter;

        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
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
            DataContext = new ViewModel(string.IsNullOrEmpty(_filter) ? null : Filter.Create(LapTimeTester.Instance, _filter));
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly IFilter<LapTimeWrapped> _filter;

            public ViewModel(IFilter<LapTimeWrapped> filter) {
                _filter = filter;
                List = WrappedCollection.Create(LapTimesManager.Instance.Entries, x => new LapTimeWrapped(x));
                View = new ListCollectionView((IList)List);

                using (View.DeferRefresh()) {
                    if (_filter == null) {
                        View.Filter = null;
                    } else {
                        View.Filter = FilterTest;
                    }
                }
            }

            private bool FilterTest(object obj) {
                var t = obj as LapTimeWrapped;
                return t != null && _filter.Test(t);
            }

            public IReadOnlyList<LapTimeWrapped> List { get; }

            public ListCollectionView View { get; }
        }
    }
}
