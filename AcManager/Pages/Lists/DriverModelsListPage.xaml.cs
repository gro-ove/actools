using System;
using System.Collections.Generic;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class DriverModelsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");

            // TODO: special filtering?
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(AcCommonObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<DriverModelObject> {
            public ViewModel(IFilter<DriverModelObject> listFilter)
                    : base(DriverModelsManager.Instance, listFilter) {}

            protected override string GetSubject() {
                return AppStrings.List_DriverModels;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<DriverModelObject>().Append(BatchAction_PackDriverModels.Instance);
        }

        public class BatchAction_PackDriverModels : CommonBatchActions.BatchAction_Pack<DriverModelObject> {
            public static readonly BatchAction_PackDriverModels Instance = new BatchAction_PackDriverModels();
            public BatchAction_PackDriverModels() : base(null) {}

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return null;
            }
        }
        #endregion
    }
}
