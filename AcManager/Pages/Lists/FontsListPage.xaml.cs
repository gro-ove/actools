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
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class FontsListPage : IParametrizedUriContent {
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

        private class ViewModel : AcListPageViewModel<FontObject> {
            public ViewModel(IFilter<FontObject> listFilter)
                : base(FontsManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_Fonts;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<FontObject>().Append(BatchAction_PackFonts.Instance);
        }

        public class BatchAction_PackFonts : CommonBatchActions.BatchAction_Pack<FontObject> {
            public static readonly BatchAction_PackFonts Instance = new BatchAction_PackFonts();
            public BatchAction_PackFonts() : base(null) {}

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return null;
            }
        }
        #endregion
    }
}
