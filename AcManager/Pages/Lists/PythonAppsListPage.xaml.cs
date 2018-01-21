using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class PythonAppsListPage : IParametrizedUriContent {
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

        private class ViewModel : AcListPageViewModel<PythonAppObject> {
            public ViewModel(IFilter<PythonAppObject> listFilter)
                : base(PythonAppsManager.Instance, listFilter) {
            }

            protected override string LoadCurrentId() {
                if (_selectNextApp != null) {
                    var value = _selectNextApp;
                    SaveCurrentKey(value);
                    _selectNextApp = null;
                    return value;
                }

                return base.LoadCurrentId();
            }

            protected override string GetSubject() {
                return AppStrings.List_Apps;
            }
        }

        public static void Show(PythonAppObject app) {
            if (Application.Current?.MainWindow is MainWindow) {
                _selectNextApp = app.Id;
                NavigateToPage();
            }
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/PythonAppsListPage.xaml", UriKind.Relative));
        }

        private static string _selectNextApp;

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<PythonAppObject>().Concat(new BatchAction[] {
                BatchAction_PackApps.Instance,
            });
        }

        public class BatchAction_PackApps : CommonBatchActions.BatchAction_Pack<PythonAppObject> {
            public static readonly BatchAction_PackApps Instance = new BatchAction_PackApps();
            public BatchAction_PackApps() : base("Batch.PackPythonApps") {}

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return new PythonAppObject.PythonAppPackerParams();
            }
        }
        #endregion
    }
}
