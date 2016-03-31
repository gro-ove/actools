using System;
using System.Windows;
using AcManager.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class CarSkinsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("CarId");
            if (id == null) {
                throw new Exception("ID is missing");
            }

            var car = CarsManager.Instance.GetById(id);
            if (car == null) {
                throw new Exception($"Car with ID “{id}” is missing");
            }

            Initialize(car, uri.GetQueryParam("Filter"));
        }

        private void Initialize([JetBrains.Annotations.NotNull] CarObject car, string filter = null) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            car.EnsureSkinsLoaded();
            DataContext = new CarSkinsListPageViewModel(car, string.IsNullOrEmpty(filter) ? null : Filter.Create(CarSkinObjectTester.Instance, filter));
            InitializeComponent();
        }

        public CarSkinsListPage([NotNull] CarObject car, string filter = null) {
            Initialize(car, filter);
        }

        public CarSkinsListPage() {}

        private void CarsListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((CarSkinsListPageViewModel)DataContext).Unload();
        }

        class CarSkinsListPageViewModel : AcListPageViewModel<CarSkinObject> {
            public CarObject SelectedCar { get; private set; }

            public CarSkinsListPageViewModel([NotNull] CarObject car, IFilter<CarSkinObject> listFilter)
                    : base(car.SkinsManager.WrappersAsIList, listFilter) {
                SelectedCar = car;
            }

            protected override string GetStatus(){
                return $"Total skins: {MainList.Count}";
            }
        }
    }
}
