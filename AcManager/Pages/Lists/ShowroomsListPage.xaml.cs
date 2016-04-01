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
    public partial class ShowroomsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ShowroomsListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ShowroomObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void ShowroomsListPage_OnLoaded(object sender, RoutedEventArgs e) {
            ((ShowroomsListPageViewModel)DataContext).Load();
        }

        private void ShowroomsListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((ShowroomsListPageViewModel)DataContext).Unload();
        }

        private class ShowroomsListPageViewModel : AcListPageViewModel<ShowroomObject> {
            public ShowroomsListPageViewModel(IFilter<ShowroomObject> listFilter)
                : base(ShowroomsManager.Instance, listFilter) {
            }

            protected override string GetStatus() => PluralizingConverter.Pluralize(MainList.Count, "{0} showroom");
        }
    }
}
