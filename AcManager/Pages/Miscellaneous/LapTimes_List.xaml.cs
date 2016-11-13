using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Miscellaneous {
    /// <summary>
    /// Interaction logic for LapTimes.xaml
    /// </summary>
    public partial class LapTimes_List : ILoadableContent, IParametrizedUriContent {
        private string _filter;

        public void OnUri(Uri uri) {
            _filter = uri.GetQueryParam("Filter");
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return LapTimesManager.Instance.UpdateAsync();
        }

        public void Load() {
            LapTimesManager.Instance.UpdateAsync().Forget();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new LapTimesViewModel(string.IsNullOrEmpty(_filter) ? null : Filter.Create(LapTimeTester.Instance, _filter));
        }

        private LapTimesViewModel Model => (LapTimesViewModel)DataContext;

        public class LapTimesViewModel : NotifyPropertyChanged {
            private readonly IFilter<LapTimeWrapped> _filter;

            public LapTimesViewModel(IFilter<LapTimeWrapped> filter) {
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
