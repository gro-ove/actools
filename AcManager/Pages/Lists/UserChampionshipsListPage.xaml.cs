using System;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Drive;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class UserChampionshipsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(AcCommonObjectTester.Instance, filter));
                    // TODO: proper filter
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<UserChampionshipObject> {
            public ViewModel(IFilter<UserChampionshipObject> listFilter)
                : base(UserChampionshipsManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return "{0} championship";
            }
        }

        protected override void OnItemDoubleClick(AcObjectNew obj) {
            var championship = obj as UserChampionshipObject;
            if (championship == null) return;
            UserChampionships.NavigateToChampionshipPage(championship);
        }
    }
}
