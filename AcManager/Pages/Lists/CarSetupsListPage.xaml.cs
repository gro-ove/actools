using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class CarSetupsListPage : IParametrizedUriContent, ILoadableContent {
        public void OnUri(Uri uri) {
            _carId = uri.GetQueryParam("CarId");
            _filter = uri.GetQueryParam("Filter");
            if (_carId == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private string _carId;
        private CarObject _car;
        private string _filter;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_car == null) {
                _car = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            await _car.SkinsManager.EnsureLoadedAsync();
        }

        public void Load() {
            if (_car == null) {
                _car = CarsManager.Instance.GetById(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            _car.SkinsManager.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new CarSetupsListPageViewModel(_car, string.IsNullOrEmpty(_filter) ? null : Filter.Create(CarSetupObjectTester.Instance, _filter));
            InitializeComponent();
        }

        public CarSetupsListPageViewModel Model => (CarSetupsListPageViewModel)DataContext;

        public class CarSetupsListPageViewModel : AcListPageViewModel<CarSetupObject> {
            public CarObject SelectedCar { get; private set; }

            public CarSetupsListPageViewModel([NotNull] CarObject car, IFilter<CarSetupObject> listFilter)
                    : base(car.SetupsManager, listFilter) {
                SelectedCar = car;
            }

            protected override string GetStatus() {
                return PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Setups);
            }
        }
    }
}
