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
    public partial class CarsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new CarsListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(CarObjectTester.Instance, filter));
            InitializeComponent();
        }

        private CarsListPageViewModel Model => (CarsListPageViewModel)DataContext;

        private void CarsListPage_OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();
        }

        private void CarsListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private class CarsListPageViewModel : AcListPageViewModel<CarObject> {
            public CarsListPageViewModel(IFilter<CarObject> listFilter)
                    : base(CarsManager.Instance, listFilter) {}

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, "{0} car");
        }
    }
}
