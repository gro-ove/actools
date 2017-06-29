using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Lists {
    public partial class CarSkinsListPage : IParametrizedUriContent, ILoadableContent {
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
            DataContext = new ViewModel(_car, string.IsNullOrEmpty(_filter) ? null : Filter.Create(CarSkinObjectTester.Instance, _filter));
            InitializeComponent();
        }

        public CarSkinsListPage([NotNull] CarObject car, string filter = null) {
            _car = car;
            _filter = filter;
        }

        public CarSkinsListPage() { }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        public class ViewModel : AcListPageViewModel<CarSkinObject> {
            public CarObject SelectedCar { get; private set; }

            public ViewModel([NotNull] CarObject car, IFilter<CarSkinObject> listFilter)
                    : base(car.SkinsManager, listFilter) {
                SelectedCar = car;
            }

            protected override string GetSubject() {
                return AppStrings.List_Skins;
            }

            private ICommand _resetPriorityCommand;

            public ICommand ResetPriorityCommand => _resetPriorityCommand ?? (_resetPriorityCommand = new AsyncCommand(async () => {
                var list = MainList.OfType<AcItemWrapper>().Select(x => x.Value as CarSkinObject).NonNull().Where(x => x.Priority.HasValue).ToList();
                var i = 0;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report();
                    await Task.Delay(500, waiting.CancellationToken);
                    if (waiting.CancellationToken.IsCancellationRequested) return;

                    foreach (var skin in list) {
                        waiting.Report(++i, list.Count);
                        skin.Priority = 0;
                        skin.Save();
                        await Task.Delay(50, waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) return;
                    }
                }
            }));
        }

        public static void Open(CarObject car) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;

            if (mainWindow == null || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                CarSkinsDialog.Show(car);
                return;
            }

            var uri = UriExtension.Create("/Pages/Lists/CarSkinsListPage.xaml?CarId={0}", car.Id);
            var setupsLinks = mainWindow.MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == "skins").ToList();
            var existing = setupsLinks.FirstOrDefault(x => x.Source == uri);
            if (existing == null) {
                existing = new LinkGroupFilterable {
                    DisplayName = $"Skins for {car.DisplayName}",
                    GroupKey = "skins",
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
