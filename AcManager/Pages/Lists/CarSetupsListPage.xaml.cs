using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using JetBrains.Annotations;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
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
            if ((Model as LocalViewModel)?.MainList.Count > 20 || (Model as RemoteViewModel)?.MainList.Count > 20) {
                FancyHints.MultiSelectionMode.Trigger();
            }
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
                    _acManager = await TheSetupMarketAsManager.CreateAsync(_car);
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
                    _acManager = TheSetupMarketAsManager.CreateAsync(_car).Result;
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

            protected override string GetSubject() {
                return AppStrings.List_Setups;
            }
        }

        public class RemoteViewModel : AcListPageViewModel<RemoteCarSetupObject> {
            public CarObject SelectedCar { get; }

            public RemoteViewModel([NotNull] CarObject car, [NotNull] IAcManagerNew manager, IFilter<RemoteCarSetupObject> listFilter)
                    : base(manager, listFilter) {
                GroupBy(nameof(RemoteCarSetupObject.TrackId), new TrackGroupDescription());
                SelectedCar = car;
            }

            protected override string GetSubject() {
                return AppStrings.List_Setups;
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

        public static void Open([NotNull] CarObject car, CarSetupsRemoteSource forceRemoteSource = CarSetupsRemoteSource.None, bool forceNewWindow = false) {
            if (forceNewWindow
                    || Keyboard.Modifiers == ModifierKeys.Control && !User32.IsAsyncKeyPressed(System.Windows.Forms.Keys.K)
                    || !(Application.Current?.MainWindow is MainWindow main) || !main.IsActive
                    || SettingsHolder.Interface.SkinsSetupsNewWindow) {
                CarSetupsDialog.Show(car, forceRemoteSource);
                return;
            }

            var remoteLinksStatus = GetRemoteLinksStatus();
            if (remoteLinksStatus != _remoteLinksStatus) {
                _remoteLinksStatus = remoteLinksStatus;
                main.MenuLinkGroups.RemoveRange(main.MenuLinkGroups.OfType<LinkGroupFilterable>().Where(x => x.GroupKey == "setups").ToList());
            }

            var existing = main.OpenSubGroup("setups", $"Setups for {car.DisplayName}",
                    UriExtension.Create("/Pages/Lists/CarSetupsListPage.xaml?CarId={0}", car.Id), FilterHints.CarSetups);
            foreach (var link in GetRemoteLinks(car.Id)) {
                existing.FixedLinks.Add(link);
            }
            if (forceRemoteSource != CarSetupsRemoteSource.None) {
                existing.SetSelected(GetRemoteSourceUri(car.Id, forceRemoteSource));
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<CarSetupObject>().Concat(new BatchAction[] {
                BatchAction_SetSetupTrack.Instance,
                BatchAction_RemoveSetupTrack.Instance,
                _remoteSource == CarSetupsRemoteSource.None ? null : BatchAction_InstallRemoteSetup.Instance
            }).NonNull();
        }

        public class BatchAction_InstallRemoteSetup : BatchAction<RemoteCarSetupObject> {
            public static readonly BatchAction_InstallRemoteSetup Instance = new BatchAction_InstallRemoteSetup();

            public BatchAction_InstallRemoteSetup() : base("Install", "Install car setups", null, "Batch.InstallRemoteSetup") {
                Priority = 10;
                DisplayApply = "Install";
            }

            public override bool IsAvailable(RemoteCarSetupObject obj) {
                return true;
            }

            private bool _asGeneric = ValuesStorage.Get<bool>("_ba.installRemoteSetup.asGeneric");

            public bool AsGeneric {
                get => _asGeneric;
                set {
                    if (Equals(value, _asGeneric)) return;
                    _asGeneric = value;
                    OnPropertyChanged();
                    ValuesStorage.Set("_ba.installRemoteSetup.asGeneric", value);
                }
            }

            protected override Task ApplyOverrideAsync(RemoteCarSetupObject obj) {
                return obj.InstallCommand.ExecuteAsync(AsGeneric ? CarSetupObject.GenericDirectory : null);
            }
        }

        public class BatchAction_SetSetupTrack : BatchAction<CarSetupObject> {
            public static readonly BatchAction_SetSetupTrack Instance = new BatchAction_SetSetupTrack();

            public BatchAction_SetSetupTrack() : base("Set track", "Assign setups to a track", "Car setup", "Batch.SetSetupTrack") {
                Track = TracksManager.Instance.GetById(ValuesStorage.Get("_ba.setSetupTrack.track", "")) ??
                        TracksManager.Instance.GetDefault();
                DisplayApply = "Set";
            }

            private TrackObject _track;

            public TrackObject Track {
                get => _track;
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                    ValuesStorage.Set("_ba.setSetupTrack.track", value?.Id);
                    RaiseAvailabilityChanged();
                }
            }

            public override bool IsAvailable(CarSetupObject obj) {
                return Track != null && obj.TrackId != Track?.Id;
            }

            private DelegateCommand _changeTrackCommand;

            public DelegateCommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                Track = SelectTrackDialog.Show(Track)?.MainTrackObject;
            }));

            protected override void ApplyOverride(CarSetupObject obj) {
                obj.Track = Track;
            }
        }

        public class BatchAction_RemoveSetupTrack : BatchAction<CarSetupObject> {
            public static readonly BatchAction_RemoveSetupTrack Instance = new BatchAction_RemoveSetupTrack();

            public BatchAction_RemoveSetupTrack() : base("Unlink from track", "Unlink setups from a track", "Car setup", null) {
                DisplayApply = "Unlink";
                Priority = -1;
            }

            public override bool IsAvailable(CarSetupObject obj) {
                return obj.TrackId != null;
            }

            protected override void ApplyOverride(CarSetupObject obj) {
                obj.Track = null;
            }
        }
        #endregion
    }
}
