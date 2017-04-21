using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
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

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
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
            DataContext = new ViewModel(_car, string.IsNullOrEmpty(_filter) ? null : Filter.Create(CarSetupObjectTester.Instance, _filter));
            InitializeComponent();
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : AcListPageViewModel<CarSetupObject> {
            public CarObject SelectedCar { get; private set; }

            public ViewModel([NotNull] CarObject car, IFilter<CarSetupObject> listFilter)
                    : base(car.SetupsManager, listFilter) {
                SelectedCar = car;
            }

            protected override string GetStatus() {
                return PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Setups);
            }
        }

        public static void Open(CarObject car) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;

            if (mainWindow == null || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                CarSetupsDialog.Show(car);
                return;
            }

            var uri = UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}", car.Id);
            var setupsLinks = mainWindow.MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == "setups").ToList();
            var existing = setupsLinks.FirstOrDefault(x => x.Source == uri);
            if (existing == null) {
                existing = new LinkGroupFilterable {
                    DisplayName = $"Setups for {car.DisplayName}",
                    GroupKey = "setups",
                    Source = uri
                };

                if (setupsLinks.Count >= 2) {
                    mainWindow.MenuLinkGroups.Remove(setupsLinks[0]);
                }

                mainWindow.MenuLinkGroups.Add(existing);
            }

            mainWindow.NavigateTo(uri);
        }
    }
}
