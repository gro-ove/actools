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
    public partial class TracksListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new TracksListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(TrackObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void TracksListPage_OnLoaded(object sender, RoutedEventArgs e) {
            ((TracksListPageViewModel)DataContext).Load();
        }

        private void TracksListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((TracksListPageViewModel)DataContext).Unload();
        }

        private class TracksListPageViewModel : AcListPageViewModel<TrackObject> {
            public TracksListPageViewModel(IFilter<TrackObject> listFilter)
                : base(TracksManager.Instance, listFilter) {
            }

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Tracks);
        }
    }
}
