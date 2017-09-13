using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ShowroomsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ShowroomObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<ShowroomObject> {
            public ViewModel(IFilter<ShowroomObject> listFilter)
                : base(ShowroomsManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_Showrooms;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<ShowroomObject>().Concat(new BatchAction[] {
                BatchAction_PackShowrooms.Instance
            });
        }

        public class BatchAction_PackShowrooms : CommonBatchActions.BatchAction_Pack<ShowroomObject> {
            public static readonly BatchAction_PackShowrooms Instance = new BatchAction_PackShowrooms();

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return null;
            }
        }
        #endregion
    }
}
