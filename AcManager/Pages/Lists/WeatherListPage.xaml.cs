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
    public partial class WeatherListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new WeatherListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(AcCommonObjectTester.Instance, filter)); // TODO: proper filter
            InitializeComponent();
        }

        private void WeatherListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((WeatherListPageViewModel)DataContext).Unload();
        }

        private class WeatherListPageViewModel : AcListPageViewModel<WeatherObject> {
            public WeatherListPageViewModel(IFilter<WeatherObject> listFilter)
                : base(WeatherManager.Instance.WrappersAsIList, listFilter) {
            }

            protected override string GetStatus() => PluralizingConverter.Pluralize(MainList.Count, "{0} weather");
        }
    }
}
