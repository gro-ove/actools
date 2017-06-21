using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
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
            _remoteSource = uri.GetQueryParamEnum<CarSetupsRemoteSource>("RemoteSource");
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
        private CarSetupsRemoteSource _remoteSource;
        private IAcManagerNew _acManager;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            if (_car == null) {
                _car = await CarsManager.Instance.GetByIdAsync(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            switch (_remoteSource) {
                case CarSetupsRemoteSource.None:
                    _acManager = _car.SetupsManager;
                    await _car.SetupsManager.EnsureLoadedAsync();
                    break;
                case CarSetupsRemoteSource.TheSetupMarket:
                    _acManager = await TheSetupMarketAsManager.CreateAsync(_car); // TODO: NULL?!
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Load() {
            if (_car == null) {
                _car = CarsManager.Instance.GetById(_carId);
                if (_car == null) throw new Exception(AppStrings.Common_CannotFindCarById);
            }

            switch (_remoteSource) {
                case CarSetupsRemoteSource.None:
                    _acManager = _car.SetupsManager;
                    _car.SetupsManager.EnsureLoaded();
                    break;
                case CarSetupsRemoteSource.TheSetupMarket:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Initialize() {
            switch (_remoteSource) {
                case CarSetupsRemoteSource.None:
                    DataContext = new LocalViewModel(_car, _acManager, string.IsNullOrEmpty(_filter) ? null : Filter.Create(CarSetupObjectTester.Instance, _filter));
                    break;
                case CarSetupsRemoteSource.TheSetupMarket:
                    DataContext = new RemoteViewModel(_car, _acManager, null); // TODO: filter?
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            InitializeComponent();
        }

        public IAcObjectListCollectionViewWrapper Model => (IAcObjectListCollectionViewWrapper)DataContext;

        private class TrackGroupDescription : GroupDescription {
            public override object GroupNameFromItem(object item, int level, CultureInfo culture) {
                var v = (item as AcItemWrapper)?.Value as ICarSetupObject;
                return v?.TrackId == null ? "" : (v.Track?.DisplayNameWithoutCount ?? $@"[{v.TrackId}]");
            }
        }

        public class LocalViewModel : AcListPageViewModel<CarSetupObject> {
            public CarObject SelectedCar { get; private set; }

            public LocalViewModel([NotNull] CarObject car, IAcManagerNew manager, IFilter<CarSetupObject> listFilter)
                    : base(manager, listFilter) {
                GroupBy(nameof(RemoteCarSetupObject.TrackId), new TrackGroupDescription());
                SelectedCar = car;
            }

            protected override string GetStatus() {
                return PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Setups);
            }
        }

        public class RemoteViewModel : AcListPageViewModel<RemoteCarSetupObject> {
            public CarObject SelectedCar { get; private set; }

            public RemoteViewModel([NotNull] CarObject car, IAcManagerNew manager, IFilter<RemoteCarSetupObject> listFilter)
                    : base(manager, listFilter) {
                GroupBy(nameof(RemoteCarSetupObject.TrackId), new TrackGroupDescription());
                SelectedCar = car;
            }

            protected override string GetStatus() {
                return PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Setups);
            }
        }

        public static Uri GetRemoteSourceUri(string carId, CarSetupsRemoteSource remoteSource) {
            return UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}&RemoteSource={1}&Special=1", carId, remoteSource);
        }

        public static int GetRemoteLinksStatus() {
            var result = 0;
            if (SettingsHolder.Integrated.TheSetupMarketTab) result += 1;
            return result;
        }

        public static IEnumerable<Link> GetRemoteLinks(string carId) {
            if (SettingsHolder.Integrated.TheSetupMarketTab) {
                yield return new Link {
                    DisplayName = "The Setup Market",
                    Source = GetRemoteSourceUri(carId, CarSetupsRemoteSource.TheSetupMarket)
                };
            }
        }

        private static int _remoteLinksStatus;

        public static void Open(CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None, bool forceNewWindow = false) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (forceNewWindow || mainWindow == null || !mainWindow.IsActive || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                CarSetupsDialog.Show(car, forceRemoteSource);
                return;
            }

            var setupsLinks = mainWindow.MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == "setups").ToList();

            var remoteLinksStatus = GetRemoteLinksStatus();
            if (remoteLinksStatus != _remoteLinksStatus) {
                _remoteLinksStatus = remoteLinksStatus;
                mainWindow.MenuLinkGroups.RemoveRange(setupsLinks);
                setupsLinks.Clear();
            }

            var uri = UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}", car.Id);
            var existing = setupsLinks.FirstOrDefault(x => x.Source == uri);
            if (existing == null) {
                existing = new LinkGroupFilterable {
                    DisplayName = $"Setups for {car.DisplayName}",
                    GroupKey = "setups",
                    Source = uri,
                    AddAllLink = true
                };

                foreach (var link in GetRemoteLinks(car.Id)) {
                    existing.FixedLinks.Add(link);
                }

                if (forceRemoteSource != CarSetupsRemoteSource.None) {
                    existing.SetSelected(GetRemoteSourceUri(car.Id, forceRemoteSource));
                }

                if (setupsLinks.Count >= 2) {
                    mainWindow.MenuLinkGroups.Remove(setupsLinks[0]);
                }

                mainWindow.MenuLinkGroups.Add(existing);
            }

            mainWindow.NavigateTo(uri);
        }
    }
}
