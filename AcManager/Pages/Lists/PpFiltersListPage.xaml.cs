using System;
using System.Collections.Generic;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class PpFiltersListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(AcCommonObjectTester.Instance, filter)); // TODO: proper filter
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<PpFilterObject> {
            public ViewModel(IFilter<PpFilterObject> listFilter)
                : base(PpFiltersManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_PpFilters;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<PpFilterObject>().Append(BatchAction_PackPpFilters.Instance);
        }

        public class BatchAction_PackPpFilters : CommonBatchActions.BatchAction_Pack<PpFilterObject> {
            public static readonly BatchAction_PackPpFilters Instance = new BatchAction_PackPpFilters();

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return null;
            }
        }
        #endregion
    }
}
