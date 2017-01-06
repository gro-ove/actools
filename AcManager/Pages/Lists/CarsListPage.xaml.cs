using System;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Windows;
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

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Cars);

            protected override string LoadCurrentId() {
                if (_selectNextCar != null) {
                    var value = _selectNextCar;
                    _selectNextCar = null;
                    return value;
                }

                return base.LoadCurrentId();
            }
        }
        
        public static void Show(CarObject car, string carSkinId = null) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car.Id;
            _selectNextCarSkinId = carSkinId;

            NavigateToPage();
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/CarsListPage.xaml", UriKind.Relative));
        }

        private static string _selectNextCar;
        private static string _selectNextCarSkinId; // TODO
    }
}
