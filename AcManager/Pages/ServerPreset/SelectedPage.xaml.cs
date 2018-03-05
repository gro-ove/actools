using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Microsoft.Win32;
using StringBasedFilter;

namespace AcManager.Pages.ServerPreset {
    public partial class SelectedPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public static ServerPresetAssistState[] AssistStates { get; } = EnumExtension.GetValues<ServerPresetAssistState>();
        public static ServerPresetJumpStart[] JumpStarts { get; } = EnumExtension.GetValues<ServerPresetJumpStart>();
        public static ServerPresetRaceJoinType[] RaceJoinTypes { get; } = EnumExtension.GetValues<ServerPresetRaceJoinType>();

        private class ProgressCapacityConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return Math.Max((value.As<double>() - 2d) / 10d, 1d);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ProgressCapacityConverter { get; } = new ProgressCapacityConverterInner();

        private class ClientsToBandwidthConverterInner : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                if (values.Length != 2) return null;
                var hz = values[0].As<double>();
                var mc = values[1].As<double>();
                return 384d * hz * mc * (mc - 1) / 1e6;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private SizeRelatedCondition _widthCondition;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _widthCondition = this.AddSizeCondition(x => x.ActualWidth > (Tab.GetLinkListWidth() ?? 360d) + 250d).Add(v => {
                Base.HeaderPadding = v ? new Thickness(0, 0, Tab.GetLinkListWidth() ?? 360, 0) : default(Thickness);
                Tab.Margin = v ? new Thickness(0, -30, 0, 0) : default(Thickness);
            });
        }

        public static IMultiValueConverter ClientsToBandwidthConverter { get; } = new ClientsToBandwidthConverterInner();

        public partial class ViewModel : SelectedAcObjectViewModel<ServerPresetObject> {
            public ServerPresetPackMode[] Modes { get; } = EnumExtension.GetValues<ServerPresetPackMode>();

            private readonly Busy _busy = new Busy();

            [CanBeNull]
            private TrackObjectBase _track;

            [CanBeNull]
            public TrackObjectBase Track {
                get => _track;
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();

                    _busy.Do(() => {
                        SelectedObject.TrackId = _track?.MainTrackObject.Id;
                        SelectedObject.TrackLayoutId = _track?.LayoutId;
                        MaximumCapacity = _track?.SpecsPitboxesValue ?? 0;
                    });
                }
            }

            private int _maximumCapacity;

            public int MaximumCapacity {
                get => _maximumCapacity;
                set => Apply(value, ref _maximumCapacity);
            }

            public BetterObservableCollection<CarObject> Cars { get; }

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                Track = SelectTrackDialog.Show(Track);
            }));

            public ViewModel([NotNull] ServerPresetObject acObject, TrackObjectBase track, CarObject[] cars) : base(acObject) {
                SelectedObject.PropertyChanged += OnAcObjectPropertyChanged;
                SelectedObject.DriverPropertyChanged += OnDriverPropertyChanged;
                SelectedObject.DriverCollectionChanged += OnDriverCollectionChanged;
                SelectedObject.WeatherCollectionChanged += OnWeatherCollectionChanged;
                SelectedObject.SaveWrapperContent += OnSaveWrapperContent;

                Track = track;
                Cars = new BetterObservableCollection<CarObject>(cars);
                Cars.CollectionChanged += OnCarsCollectionChanged;

                InitializeWrapperContent();
                UpdateWrapperContentCars();
                UpdateWrapperContentTracks();
                UpdateWrapperContentWeather();
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= OnAcObjectPropertyChanged;
                SelectedObject.DriverPropertyChanged -= OnDriverPropertyChanged;
                SelectedObject.DriverCollectionChanged -= OnDriverCollectionChanged;
                SelectedObject.WeatherCollectionChanged -= OnWeatherCollectionChanged;
                SelectedObject.SaveWrapperContent -= OnSaveWrapperContent;
                _helper.Dispose();
                PackServerPresets = null;
            }

            private IEnumerable<WrapperContentObject> Wrappers() {
                return WrapperContentCars.SelectMany(x => x.Children)
                                         .Concat(WrapperContentCars)
                                         .Concat(WrapperContentTracks)
                                         .Concat(WrapperContentWeather);
            }

            private void OnSaveWrapperContent(object sender, EventArgs eventArgs) {
                var f = Wrappers().Select(x => x.Filename).NonNull().Distinct().ToList();
                var l = Wrappers().SelectMany(x => x.GetFilesToRemove().Where(y => f.All(z => !FileUtils.ArePathsEqual(y, z)))).ToArray();
                if (l.Length == 0) return;

                Logging.Debug("To recycle:\n" + l.JoinToString('\n'));
                FileUtils.Recycle(l);
            }

            private AsyncCommand _wrapperRepackAllCommand;

            public AsyncCommand WrapperRepackAllCommand => _wrapperRepackAllCommand ?? (_wrapperRepackAllCommand = new AsyncCommand(async () => {
                using (var waiting = WaitingDialog.Create("Repacking…")) {
                    var list = Wrappers().Where(x => x.ShareMode == ShareMode.Directly).ToList();
                    for (var i = 0; i < list.Count; i++) {
                        var w = list[i];
                        waiting.Report(w.DisplayName, i, list.Count);
                        await w.Repack(waiting.Subrange((double)i / list.Count, 1d / list.Count, $"Packing: {w.DisplayName} ({{0}})…", false),
                                waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) return;
                    }
                }
            }));

            private AsyncCommand _wrapperRemoveUnusedCommand;

            public AsyncCommand WrapperRemoveUnusedCommand => _wrapperRemoveUnusedCommand ?? (_wrapperRemoveUnusedCommand = new AsyncCommand(async () => {
                try {
                    using (WaitingDialog.Create("Recycling…")) {
                        await Task.Run(() => {
                            var f = Wrappers().Select(x => x.Filename).NonNull().Distinct().ToList();
                            var l = Directory.GetFiles(SelectedObject.WrapperContentDirectory).Where(x => !string.Equals(Path.GetFileName(x), "content.json",
                                    StringComparison.OrdinalIgnoreCase) && f.All(y => !FileUtils.ArePathsEqual(x, y))).ToArray();
                            if (l.Length == 0) return;

                            Logging.Debug("To recycle:\n" + l.JoinToString('\n'));
                            FileUtils.Recycle(l);
                        });
                    }
                } catch (Exception e) {
                    Logging.Debug(e);
                }
            }));

            private void OnDriverCollectionChanged(object sender, EventArgs eventArgs) {
                UpdateWrapperContentCars();
            }

            private void OnWeatherCollectionChanged(object sender, EventArgs eventArgs) {
                UpdateWrapperContentWeather();
            }

            private BetterListCollectionView _savedDrivers;

            public BetterListCollectionView SavedDrivers {
                get {
                    if (_savedDrivers == null) {
                        if (!_savedDriversFilterSet) {
                            SavedDriversFilter = ValuesStorage.Get<string>(KeySavedDriversFilter);
                        }

                        _savedDrivers = new BetterListCollectionView(ServerPresetsManager.Instance.SavedDrivers);
                        using (_savedDrivers.DeferRefresh()) {
                            _savedDrivers.SortDescriptions.Add(new SortDescription(nameof(ServerSavedDriver.DriverName), ListSortDirection.Ascending));
                            _savedDrivers.Filter = SavedDriversFilterFn;
                        }
                    }

                    return _savedDrivers;
                }
            }

            private bool SavedDriversFilterFn(object o) {
                return o is ServerSavedDriver d && _savedDriversFilterObj?.Test(d) != false;
            }

            private IFilter<ServerSavedDriver> _savedDriversFilterObj;

            private class SavedDriverTester : IParentTester<ServerSavedDriver> {
                public static readonly SavedDriverTester Instance = new SavedDriverTester();

                public string ParameterFromKey(string key) {
                    switch (key) {
                        case "n":
                        case "name":
                        case "driver":
                            return nameof(ServerSavedDriver.DriverName);

                        case "t":
                        case "team":
                            return nameof(ServerSavedDriver.TeamName);

                        case "g":
                        case "id":
                        case "guid":
                            return nameof(ServerSavedDriver.Guid);
                    }

                    return null;
                }

                public bool Test(ServerSavedDriver obj, string key, ITestEntry value) {
                    if (key == null) {
                        return value.Test(obj.DriverName) || value.Test(obj.Guid) || value.Test(obj.TeamName);
                    }

                    switch (key) {
                        case "n":
                        case "name":
                        case "driver":
                            return value.Test(obj.DriverName);

                        case "t":
                        case "team":
                            return value.Test(obj.TeamName);

                        case "g":
                        case "id":
                        case "guid":
                            return value.Test(obj.Guid);

                        case "car":
                            var id = obj.GetCarId();
                            if (id == null) return false;
                            if (value.Test(id)) return true;

                            var c = CarsManager.Instance.GetById(id);
                            return c != null && value.Test(c.DisplayName);
                    }

                    return false;
                }

                public bool TestChild(ServerSavedDriver obj, string key, IFilter filter) {
                    switch (key) {
                        case null:
                        case "car":
                            var id = obj.GetCarId();
                            if (id == null) return false;

                            var c = CarsManager.Instance.GetById(id);
                            return c != null && filter.Test(CarObjectTester.Instance, c);
                    }

                    return false;
                }
            }

            private static readonly string KeySavedDriversFilter = "__SavedDriversFilterValue";
            private bool _savedDriversFilterSet;
            private string _savedDriversFilter;

            public string SavedDriversFilter {
                get => _savedDriversFilter;
                set {
                    if (Equals(value, _savedDriversFilter)) return;
                    _savedDriversFilter = value;
                    _savedDriversFilterSet = true;
                    OnPropertyChanged();

                    _savedDriversFilterObj = Filter.Create(SavedDriverTester.Instance, value);
                    _savedDrivers?.Refresh();

                    ValuesStorage.Set(KeySavedDriversFilter, value);
                }
            }

            private void OnCarsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                _busy.Do(() => {
                    SelectedObject.CarIds = Cars.Select(x => x.Id).ToArray();
                });
            }

            private DelegateCommand _changeWelcomeMessagePathCommand;

            public DelegateCommand ChangeWelcomeMessagePathCommand => _changeWelcomeMessagePathCommand ?? (_changeWelcomeMessagePathCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.TextFilter,
                    Title = "Select new welcome message",
                    InitialDirectory = Path.GetDirectoryName(SelectedObject.WelcomeMessagePath) ?? "",
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    SelectedObject.WelcomeMessagePath = dialog.FileName;
                }
            }));

            private void OnAcObjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(SelectedObject.TrackId):
                    case nameof(SelectedObject.TrackLayoutId):
                        _busy.Do(() => {
                            Track = TracksManager.Instance.GetLayoutById(SelectedObject.TrackId, SelectedObject.TrackLayoutId);
                        });
                        UpdateWrapperContentTracks();
                        break;

                    case nameof(SelectedObject.CarIds):
                        _busy.Do(() => {
                            Cars.ReplaceEverythingBy(SelectedObject.CarIds.Select(x => CarsManager.Instance.GetById(x)));
                        });
                        break;

                    case nameof(SelectedObject.DriverEntries):
                        UpdateWrapperContentCars();
                        break;

                    case nameof(SelectedObject.WrapperContentJObject):
                        _wrapperContentCarsBusy.Do(LoadWrapperContentCars);
                        _wrapperContentTracksBusy.Do(LoadWrapperContentTracks);
                        _wrapperContentWeatherBusy.Do(LoadWrapperContentWeather);
                        break;
                }
            }

            private void OnDriverPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(ServerPresetDriverEntry.CarId):
                    case nameof(ServerPresetDriverEntry.CarSkinId):
                        UpdateWrapperContentCars();
                        break;
                }
            }

            private AsyncCommand _packCommand;

            public AsyncCommand PackCommand => _packCommand ?? (_packCommand = new AsyncCommand(() => {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) {
                    new PackServerDialog(SelectedObject).ShowDialog();
                    return Task.Delay(0);
                }

                return new PackServerDialog.ViewModel(null, SelectedObject).PackCommand.ExecuteAsync();
            }));

            private DelegateCommand _packOptionsCommand;

            public DelegateCommand PackOptionsCommand => _packOptionsCommand ?? (_packOptionsCommand = new DelegateCommand(() => {
                new PackServerDialog(SelectedObject).ShowDialog();
            }));

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand = new AsyncCommand(async () => {
                try {
                    using (var waiting = new WaitingDialog()) {
                        // await PackServerDialog.EnsurePacked(SelectedObject, waiting);
                        // if (waiting.CancellationToken.IsCancellationRequested) return;
                        await SelectedObject.RunServer(waiting, waiting.CancellationToken);
                    }
                } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                    NonfatalError.Notify("Can’t run server", e);
                }
            }, () => SelectedObject.RunServerCommand.IsAbleToExecute).ListenOnWeak(SelectedObject.RunServerCommand));

            private AsyncCommand _restartCommand;

            public AsyncCommand RestartCommand => _restartCommand ?? (_restartCommand = new AsyncCommand(() => {
                SelectedObject.StopServer();
                GoCommand.ExecuteAsync().Forget();
                return Task.Delay(0);
            }, () => SelectedObject.RestartServerCommand.IsAbleToExecute).ListenOnWeak(SelectedObject.RestartServerCommand));

            private HierarchicalItemsView _packServerPresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public HierarchicalItemsView PackServerPresets {
                get => _packServerPresets;
                set => Apply(value, ref _packServerPresets);
            }

            public void InitializePackServerPresets() {
                if (PackServerPresets == null) {
                    PackServerPresets = _helper.Create(new PresetsCategory(PackServerDialog.ViewModel.PresetableKeyValue), p => {
                        new PackServerDialog.ViewModel(p.ReadData(), SelectedObject).PackCommand.ExecuteAsync().Forget();
                    });
                }
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private ServerPresetObject _object;
        private TrackObjectBase _track;
        private CarObject[] _cars;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ServerPresetsManager.Instance.GetByIdAsync(_id);
            if (_object == null) return;

            _track = _object.TrackId == null ? null : await TracksManager.Instance.GetLayoutByIdAsync(_object.TrackId, _object.TrackLayoutId);
            _cars = (await _object.CarIds.Select(x => CarsManager.Instance.GetByIdAsync(x)).WhenAll(4)).ToArray();
        }

        void ILoadableContent.Load() {
            _object = ServerPresetsManager.Instance.GetById(_id);
            if (_object == null) return;

            _track = _object.TrackId == null ? null : TracksManager.Instance.GetLayoutById(_object.TrackId, _object.TrackLayoutId);
            _cars = _object.CarIds.Select(x => CarsManager.Instance.GetById(x)).ToArray();
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            if (SettingsHolder.Online.ServerPresetsFitInFewerTabs) {
                Tab.Links.Remove(MainBasicLink);
                Tab.Links.Remove(AssistsLink);
                Tab.Links.Remove(ConditionsLink);
                Tab.Links.Remove(SessionsLink);
            } else {
                Tab.Links.Remove(MainCombinedLink);
            }

            SetModel();
            InitializeComponent();

            this.OnActualUnload(() => {
                _object?.UnsubscribeWeak(OnServerPropertyChanged);
            });
        }

        public bool ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = ServerPresetsManager.Instance.GetById(id);
            if (obj == null) return false;

            var track = obj.TrackId == null ? null : TracksManager.Instance.GetLayoutById(obj.TrackId, obj.TrackLayoutId);
            var cars = obj.CarIds.Select(x => CarsManager.Instance.GetById(x)).ToArray();

            _id = id;
            _object?.UnsubscribeWeak(OnServerPropertyChanged);
            _object = obj;
            _track = track;
            _cars = cars;

            SetModel();
            _widthCondition?.UpdateAfterRender();
            return true;
        }

        private void SetModel() {
            _model?.Unload();
            _object.SubscribeWeak(OnServerPropertyChanged);
            RunningLogLink.IsShown = _object.RunningLog != null;
            InitializeAcObjectPage(_model = new ViewModel(_object, _track, _cars));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.RestartCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.PackCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.PackOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift))
            });

            foreach (var binding in Enumerable.Range(0, 8).Select(i => new InputBinding(new DelegateCommand(() => {
                Tab.SelectedSource = Tab.Links.ApartFrom(RunningLogLink).ElementAtOrDefault(i)?.Source ?? Tab.SelectedSource;
            }), new KeyGesture(Key.F1 + i, ModifierKeys.Alt | ModifierKeys.Control)))) {
                InputBindings.Add(binding);
            }
        }

        private void OnServerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ServerPresetObject.RunningLog):
                    RunningLogLink.IsShown = _object.RunningLog != null;
                    _widthCondition?.UpdateAfterRender();
                    break;
            }
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e) {
            var source = Tab.SelectedSource;
            var runningLogTab = source == TryFindResource(@"RunningLogUri") as Uri;
            var entryListTab = !runningLogTab && source == TryFindResource(@"EntryListUri") as Uri;
            var wrappedTab = !runningLogTab && !entryListTab && source == TryFindResource(@"WrappedUri") as Uri;

            IsRunningMessage.Visibility = runningLogTab ? Visibility.Collapsed : Visibility.Visible;
            RandomizeSkinsButton.Visibility = entryListTab ? Visibility.Visible : Visibility.Collapsed;
            RemoveEntriesButton.Visibility = entryListTab ? Visibility.Visible : Visibility.Collapsed;
            ClearUnusedArchivesButton.Visibility = wrappedTab ? Visibility.Visible : Visibility.Collapsed;
            RepackAllArchivesButton.Visibility = wrappedTab ? Visibility.Visible : Visibility.Collapsed;
            ExtraButtonsSeparator.Visibility = entryListTab || wrappedTab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPackServerButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializePackServerPresets();
        }
    }
}
