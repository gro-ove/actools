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
    public partial class ServerPresetsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ServerPresetObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<ServerPresetObject> {
            public ViewModel(IFilter<ServerPresetObject> listFilter) : base(ServerPresetsManager.Instance, listFilter) {}

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_ServerPresets);
        }
    }
}
