using System;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class PpFiltersListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new PpFiltersListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(AcCommonObjectTester.Instance, filter)); // TODO: proper filter
            InitializeComponent();
        }

        private void PpFiltersListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((PpFiltersListPageViewModel)DataContext).Unload();
        }

        private class PpFiltersListPageViewModel : AcListPageViewModel<PpFilterObject> {
            public PpFiltersListPageViewModel(IFilter<PpFilterObject> listFilter)
                : base(PpFiltersManager.Instance, listFilter) {
            }

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, "{0} filter");
        }
    }
}
