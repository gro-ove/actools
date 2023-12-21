using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AcManager.ContentRepair;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.DiscordRpc;
using AcManager.Pages.ContentTools;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Lists;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using StringBasedFilter;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive : ILoadableContent {
        public const string PresetableKeyValue = "Quick Drive";
        private const string KeySaveable = "__QuickDrive_Main";

        private const string ModeDriftPath = "/Pages/Drive/QuickDrive_Drift.xaml";
        private const string ModeHotlapPath = "/Pages/Drive/QuickDrive_Hotlap.xaml";
        private const string ModePracticePath = "/Pages/Drive/QuickDrive_Practice.xaml";
        private const string ModeRacePath = "/Pages/Drive/QuickDrive_Race.xaml";
        private const string ModeTrackdayPath = "/Pages/Drive/QuickDrive_Trackday.xaml";
        private const string ModeWeekendPath = "/Pages/Drive/QuickDrive_Weekend.xaml";
        private const string ModeTimeAttackPath = "/Pages/Drive/QuickDrive_TimeAttack.xaml";
        private const string ModeDragPath = "/Pages/Drive/QuickDrive_Drag.xaml";
        private const string ModeCustomPath = "/Pages/Drive/QuickDrive_Custom.xaml";

        public static readonly Uri ModeDrift = new Uri(ModeDriftPath, UriKind.Relative);
        public static readonly Uri ModeHotlap = new Uri(ModeHotlapPath, UriKind.Relative);
        public static readonly Uri ModePractice = new Uri(ModePracticePath, UriKind.Relative);
        public static readonly Uri ModeRace = new Uri(ModeRacePath, UriKind.Relative);
        public static readonly Uri ModeTrackday = new Uri(ModeTrackdayPath, UriKind.Relative);
        public static readonly Uri ModeWeekend = new Uri(ModeWeekendPath, UriKind.Relative);
        public static readonly Uri ModeTimeAttack = new Uri(ModeTimeAttackPath, UriKind.Relative);
        public static readonly Uri ModeDrag = new Uri(ModeDragPath, UriKind.Relative);

        private ViewModel Model => (ViewModel)DataContext;
        private readonly DiscordRichPresence _discordPresence = new DiscordRichPresence(10, "Preparing to race", "Quick Drive");

        public Task LoadAsync(CancellationToken cancellationToken) {
            return WeatherManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            WeatherManager.Instance.EnsureLoaded();
        }

        private static WeakReference<QuickDrive> _current;
        private static int _something = 20 * 60 * 60 * 1000;

        public void Initialize() {
            if (_selectNextCarSkinId != null && _selectNextCar != null) {
                _selectNextCar.SelectedSkin = _selectNextCar.EnabledOnlySkins.GetByIdOrDefault(_selectNextCarSkinId) ?? _selectNextCar.SelectedSkin;
            }

            DataContext = new ViewModel(_selectNextSerializedPreset, true,
                    _selectNextCar, null, //_selectNextCarSkinId,
                    track: _selectNextTrack, trackSkin: _selectNextTrackSkin,
                    weatherId: _selectNextWeather?.Id, time: _selectNextTime,
                    mode: _selectNextMode, serializedRaceGrid: _selectNextSerializedRaceGrid,
                    presence: _discordPresence, forceAssistsLoading: _selectNextForceAssistsLoading);

            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(Model.TrackState, nameof(INotifyPropertyChanged.PropertyChanged),
                    OnTrackStateChanged);
            WeakEventManager<NewRaceModeData.Holder, EventArgs>.AddHandler(NewRaceModeData.Instance, nameof(NewRaceModeData.Holder.Reloaded),
                    OnNewModesChanged);
            this.OnActualUnload(() => {
                _discordPresence.Dispose();
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(Model.TrackState,
                        nameof(INotifyPropertyChanged.PropertyChanged), OnTrackStateChanged);
                WeakEventManager<NewRaceModeData.Holder, EventArgs>.RemoveHandler(NewRaceModeData.Instance, nameof(NewRaceModeData.Holder.Reloaded),
                        OnNewModesChanged);
                Model.Dispose();
            });

            if (NewRaceModeData.Instance.IsReady) {
                OnNewModesChanged(null, null);
            }

            _current = new WeakReference<QuickDrive>(this);

            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => {
                    var selectedCar = Model.SelectedCar;
                    if (selectedCar == null) return;
                    CustomShowroomWrapper.StartAsync(selectedCar, selectedCar.SelectedSkin);
                }), new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(new DelegateCommand(() => {
                    var selectedCar = Model.SelectedCar;
                    if (selectedCar == null) return;
                    CarOpenInShowroomDialog.Run(selectedCar, selectedCar.SelectedSkin?.Id);
                }), new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(new DelegateCommand(() => {
                    var selectedCar = Model.SelectedCar;
                    if (selectedCar == null) return;
                    new CarOpenInShowroomDialog(selectedCar, selectedCar.SelectedSkin?.Id).ShowDialog();
                }), new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.RandomizeCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarSkinCommand, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarCommand, new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTrackCommand, new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTimeCommand, new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomWeatherCommand, new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTemperatureCommand, new KeyGesture(Key.D5, ModifierKeys.Control | ModifierKeys.Alt)),
#if DEBUG
                new InputBinding(new DelegateCommand(() => {
                    var selectedCar = Model.SelectedCar;
                    var track = Model.SelectedTrack;
                    if (selectedCar == null || track == null) return;
                    LapTimesManager.Instance.AddEntry(selectedCar.Id, track.IdWithLayout, DateTime.Now, TimeSpan.FromMilliseconds(_something -= 193)).Ignore();
                }), new KeyGesture(Key.L, ModifierKeys.Alt | ModifierKeys.Control)),
#endif
            });

            _selectNextSerializedPreset = null;
            _selectNextCar = null;
            _selectNextCarSkinId = null;
            _selectNextTrack = null;
            _selectNextTrackSkin = null;
            _selectNextWeather = null;
            _selectNextMode = null;
            _selectNextSerializedRaceGrid = null;
            _selectNextForceAssistsLoading = false;
            _selectNextTime = null;

            if (AppArguments.Has(AppFlag.SimpleQuickDriveMode)) {
                Content = FindResource("SimpleVersion");
            } else {
                this.AddSizeCondition(x => x.ActualHeight > 600 && SettingsHolder.Drive.ShowExtraComboBoxes).Add(CarCellExtra).Add(TrackCellExtra).Add(x => {
                    if (!_loaded && ValuesStorage.Contains(".qd.rz.h")) {
                        CarCellBase.Height = TrackCellBase.Height = ValuesStorage.Get(".qd.rz.h", CarCellBase.Height);
                    }  else {
                        CarCellBase.Height = TrackCellBase.Height = 120d;
                        ValuesStorage.Remove(".qd.rz.h");
                    } 
                });
                this.AddSizeCondition(x => (((ActualWidth - 720) / 440).Saturate() * 93 + 120).Round()).Add(x => {
                    if (!_loaded && ValuesStorage.Contains(".qd.rz.w")) {
                        CarCell.Width = TrackCell.Width = ValuesStorage.Get(".qd.rz.w", CarCell.Width);
                    } else {
                        CarCell.Width = TrackCell.Width = x;
                        ValuesStorage.Remove(".qd.rz.w");
                    }
                });
                this.AddSizeCondition(x => 180 + ((x.ActualWidth - 800) / 2d).Clamp(0, 60).Round()).Add(x => LeftPanel.Width = x);
                Loaded += (sender, args) => _loaded = true;
            }
        }

        private bool _loaded = false;

        private void OnTrackStateChanged(object sender, PropertyChangedEventArgs e) {
            Model.SaveLater();
        }

        private DispatcherTimer _realConditionsTimer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (Model.SelectedWeather is WeatherTypeWrapped w) {
                w.RefreshReference();
            }
            
            if (_realConditionsTimer != null) return;

            _realConditionsTimer = new DispatcherTimer();
            _realConditionsTimer.Tick += (o, args) => {
                if (Model.RealConditions) {
                    Model.UpdateConditions();
                }
            };
            _realConditionsTimer.Interval = TimeSpan.FromMinutes(0.5);
            _realConditionsTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_realConditionsTimer == null) return;
            _realConditionsTimer.Stop();
            _realConditionsTimer = null;
        }

        private void OnModeTabNavigated(object sender, NavigationEventArgs e) {
            if (ModeTab.Frame.Content is IQuickDriveModeControl c) {
                c.Model = Model.SelectedModeViewModel;
            }

            // _model.SelectedModeViewModel = (ModeTab.Frame.Content as IQuickDriveModeControl)?.Model;
        }

        private class SaveableData {
            public Uri Mode;
            public string ModeData, CarId, TrackId, WeatherId;
            public bool RealConditions;
            public double Temperature;
            public int Time, TimeMultipler;

            [JsonProperty(@"udt")]
            public bool UseSpecificDate;

            [JsonProperty(@"dtv")]
            public DateTime? SpecificDateValue;

            [JsonProperty(@"tpc")]
            public bool TrackPropertiesChanged;

            public string TrackPropertiesPresetFilename, TrackPropertiesData;

            [JsonProperty(@"asc")]
            public bool AssistsChanged;

            public string AssistsPresetFilename, AssistsData;

            [JsonProperty(@"ico")]
            public bool IdealConditions;

            [JsonProperty(@"wsf")]
            public double WindSpeedMin = 10;

            [JsonProperty(@"wst")]
            public double WindSpeedMax = 10;

            [JsonProperty(@"wd")]
            public double WindDirection;

            [JsonProperty(@"rws")]
            public bool RandomWindSpeed;

            [JsonProperty(@"rwd")]
            public bool RandomWindDirection;

            // Obsolete
            [JsonProperty(@"TrackPropertiesPreset")]
#pragma warning disable 649
            public string ObsTrackPropertiesPreset;
#pragma warning restore 649

            [JsonProperty(@"rcTimezones")]
            public bool? RealConditionsTimezones;

            [JsonProperty(@"rcManTime")]
            public bool? RealConditionsManualTime;

            [JsonProperty(@"rcManWind")]
            public bool? RealConditionsManualWind;

            [JsonProperty(@"rcLw")]
            public bool? RealConditionsLocalWeather;

            [JsonProperty(@"rte")]
            public bool RandomTemperature;

            [JsonProperty(@"rti")]
            public bool RandomTime;

            [JsonProperty(@"crt")]
            public bool CustomRoadTemperature;

            [JsonProperty(@"crtv")]
            public double? CustomRoadTemperatureValue;
        }

        private void OnNewModesChanged(object sender, EventArgs e) {
            LinksList.Children.ReplaceEverythingBy(LinksList.Children.Where(x => x.Source?.OriginalString.StartsWith(ModeCustomPath) == false)
                    .Concat(NewRaceModeData.Instance.Items.Select(x => new Link {
                        DisplayName = x.DisplayName,
                        Source = new Uri(ModeCustomPath + "?Id=" + x.Id, UriKind.Relative),
                        ToolTip = x.GetToolTip()
                    })));
        }

        public partial class ViewModel : NotifyPropertyChanged, IUserPresetable {
            private readonly bool _uiMode;

            #region Notifieable Stuff
            private Uri _selectedMode;
            private CarObject _selectedCar;
            private TrackObjectBase _selectedTrack;

            private static Dictionary<string, Func<bool, QuickDriveModeViewModel>> _knownModes = new Dictionary<string, Func<bool, QuickDriveModeViewModel>> {
                [ModeDriftPath] = initialize => new QuickDrive_Drift.ViewModel(initialize),
                [ModeHotlapPath] = initialize => new QuickDrive_Hotlap.ViewModel(initialize),
                [ModePracticePath] = initialize => new QuickDrive_Practice.ViewModel(initialize),
                [ModeRacePath] = initialize => new QuickDrive_Race.ViewModel(initialize),
                [ModeTrackdayPath] = initialize => new QuickDrive_Trackday.ViewModel(initialize),
                [ModeWeekendPath] = initialize => new QuickDrive_Weekend.ViewModel(initialize),
                [ModeTimeAttackPath] = initialize => new QuickDrive_TimeAttack.ViewModel(initialize),
                [ModeDragPath] = initialize => new QuickDrive_Drag.ViewModel(initialize),
            };

            private bool _skipLoading;

            public Uri SelectedMode {
                get => _selectedMode;
                set {
                    if (value.OriginalString.StartsWith(ModeCustomPath) && !NewRaceModeData.Instance.IsReady) {
                        WaitForNewModes(value).Ignore();
                        return;
                    }
                    
                    if (Equals(value, _selectedMode)) return;
                    _selectedMode = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (_knownModes.TryGetValue(value.OriginalString, out var constructor)) {
                        SelectedModeViewModel = constructor(!_skipLoading);
                    } else if (value.OriginalString.StartsWith(ModeCustomPath)) {
                        try {
                            SelectedModeViewModel = new QuickDrive_Custom.ViewModel(value.GetQueryParam("Id"), !_skipLoading);
                        } catch (Exception e) {
                            NonfatalError.Notify("Failed to configure custom mode", e);
                            SelectedMode = ModePractice;
                        }
                    } else {
                        Logging.Warning("Unknown mode: " + value);
                        SelectedMode = ModePractice;
                    }
                }
            }

            private async Task WaitForNewModes(Uri uri) {
                for (var i = 0; i < 5 && !NewRaceModeData.Instance.IsReady; ++i) {
                    await Task.Delay(200);
                }
                SelectedMode = uri;
            }

            private AsyncCommand _randomizeCommand;

            public AsyncCommand RandomizeCommand => _randomizeCommand ?? (_randomizeCommand = new AsyncCommand(async () => {
                RandomCarCommand.Execute();
                await Task.Delay(100);
                RandomTrackCommand.Execute();
                await Task.Delay(100);
                RandomTimeCommand.Execute();
                await Task.Delay(100);
                SetRandomWeather(false);
                await Task.Delay(100);
                RandomTemperatureCommand.Execute();
            }));

            private CarObject _previousTunableParent;

            public void Dispose() {
                CarsManager.Instance.WrappersList.ItemPropertyChanged -= OnListItemPropertyChanged;
                CarsManager.Instance.WrappersList.WrappedValueChanged -= OnListWrappedValueChanged;
                WeatherManager.Instance.WrappersList.CollectionChanged -= OnWeatherListChanged;
                WeatherManager.Instance.WrappersList.WrappedValueChanged -= OnWeatherListChanged;
                WeatherManager.Instance.WrappersList.ItemPropertyChanged -= OnWeatherPropertyChanged;
            }

            private readonly Busy _updateTimeDiapason = new Busy();

            private void OnWeatherListChanged(object sender, EventArgs e) {
                _updateTimeDiapason.Yield(UpdateTimeDiapason);
            }

            private void OnWeatherPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(WeatherObject.TimeDiapason)) {
                    _updateTimeDiapason.Yield(UpdateTimeDiapason);
                }
            }

            private void UpdateTunableVersions(CarObject selected) {
                if (!_uiMode || !SettingsHolder.Drive.ShowExtraComboBoxes) return;

                if (selected == null) {
                    TunableVersions.Clear();
                    HasChildren = false;
                    _previousTunableParent = null;
                    return;
                }

                var parent = selected.ParentId == null ? selected : selected.Parent;
                if (parent == _previousTunableParent) {
                    return;
                }

                if (parent == null) {
                    TunableVersions.Clear();
                    HasChildren = false;
                    _previousTunableParent = null;
                    return;
                }

                _previousTunableParent = parent;

                var children = parent.Children.Where(x => x.Enabled).ToList();
                HasChildren = children.Any();

                if (!HasChildren) {
                    TunableVersions.Clear();
                    return;
                }

                TunableVersions.ReplaceEverythingBy_Direct(new[] { parent }.Where(x => x.Enabled).Concat(children));
            }

            private void OnListItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(CarObject.ParentId) && sender is CarObject car && SelectedCar != null && car.ParentId == SelectedCar.Id) {
                    _previousTunableParent = null;
                    UpdateTunableVersions(SelectedCar);
                }
            }

            private void OnListWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
                if (e.NewValue is CarObject car && SelectedCar != null && car.ParentId == SelectedCar.Id) {
                    _previousTunableParent = null;
                    UpdateTunableVersions(SelectedCar);
                }
            }

            private bool _hasChildren;

            public bool HasChildren {
                get => _hasChildren;
                set => Apply(value, ref _hasChildren);
            }

            public BetterObservableCollection<CarObject> TunableVersions { get; } = new BetterObservableCollection<CarObject>();

            [CanBeNull]
            public CarObject SelectedCar {
                get => _selectedCar;
                set {
                    if (Equals(value, _selectedCar)) return;

                    if (_selectedCar != null && _selectedCar.Author != AcCommonObject.AuthorKunos && _selectedCar.AcdData != null) {
                        WeakEventManager<DataWrapper, DataChangedEventArgs>.RemoveHandler(_selectedCar.AcdData, nameof(DataWrapper.DataChanged),
                                OnCarDataChanged);
                    }

                    UpdateTunableVersions(value);
                    _selectedCar = value;
                    // _selectedCar?.LoadSkins();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();

                    AcContext.Instance.CurrentCar = value;
                    _presence?.Car(value);

                    if (value != null && value.Author != AcCommonObject.AuthorKunos) {
                        SelectedCarRepairSuggestions = CarRepair.GetRepairSuggestions(value, true, true, true).ToList();
                        if (value.AcdData != null) {
                            WeakEventManager<DataWrapper, DataChangedEventArgs>.AddHandler(value.AcdData, nameof(DataWrapper.DataChanged),
                                    OnCarDataChanged);
                        }
                    } else {
                        SelectedCarRepairSuggestions = null;
                    }
                }
            }

            private void OnCarDataChanged(object sender, DataChangedEventArgs dataChangedEventArgs) {
                var car = SelectedCar;
                SelectedCarRepairSuggestions = car != null && car.Author != AcCommonObject.AuthorKunos ?
                        CarRepair.GetRepairSuggestions(car, true, true, true).ToList() : null;
            }

            private List<ContentRepairSuggestion> _selectedCarRepairSuggestions;

            [CanBeNull]
            public List<ContentRepairSuggestion> SelectedCarRepairSuggestions {
                get => _selectedCarRepairSuggestions;
                set => Apply(value, ref _selectedCarRepairSuggestions);
            }

            private DelegateCommand _repairCarCommand;

            public DelegateCommand RepairCarCommand => _repairCarCommand ?? (_repairCarCommand = new DelegateCommand(() => {
                if (SelectedCar != null) {
                    CarAnalyzer.Run(SelectedCar);
                }
            }));

            [CanBeNull]
            private static T GetRandomObject<T>(BaseAcManager<T> manager, [CanBeNull] string currentId, [CanBeNull] IFilter<T> filter) where T : AcObjectNew {
                var selection = manager.WrappersList.Where(x => x.Value.Enabled).Select(x => x.Id)
                        .ApartFrom(currentId);
                if (filter != null) {
                    var candidates = selection.ToList();
                    for (var i = 0; i < 3; ++i) {
                        var random = candidates.RandomElementOrDefault();
                        if (random != null) {
                            var candidate = manager.GetById(random);
                            if (candidate != null && filter.Test(candidate)) {
                                return candidate;
                            }
                        }
                    }
                    return candidates.Select(manager.GetById).NonNull().Where(filter.Test).RandomElementOrDefault()
                            ?? (currentId != null ? manager.GetById(currentId) : null) ?? manager.GetDefault();
                }
                var id = selection.RandomElementOrDefault()
                        ?? currentId ?? string.Empty;
                return manager.GetById(id) ?? manager.GetDefault();
            }

            private DelegateCommand _randomCarCommand;

            public DelegateCommand RandomCarCommand => _randomCarCommand ?? (_randomCarCommand = new DelegateCommand(() => {
                SelectedCar = GetRandomObject(CarsManager.Instance, SelectedCar?.Id,
                        string.IsNullOrWhiteSpace(SettingsHolder.Drive.QuickDriveRandomizeCarFilter) ? null
                                : Filter.Create(CarObjectTester.Instance, SettingsHolder.Drive.QuickDriveRandomizeCarFilter));
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id, null);
                }
            }));

            private DelegateCommand _randomCarSkinCommand;

            public DelegateCommand RandomCarSkinCommand => _randomCarSkinCommand ?? (_randomCarSkinCommand = new DelegateCommand(() => {
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id, null);
                }
            }));

            public object AlwaysNull {
                get => "";
                set => OnPropertyChanged();
            }

            public TrackStateViewModel TrackState => TrackStateViewModel.Instance;

            [CanBeNull]
            public TrackObjectBase SelectedTrack {
                get => _selectedTrack;
                set {
                    if (Equals(value, _selectedTrack)) return;
                    _selectedTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();
                    TrackUpdated();
                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                    AcContext.Instance.CurrentTrack = value;
                    _presence?.Track(value);
                }
            }

            private DelegateCommand _randomTrackCommand;

            public DelegateCommand RandomTrackCommand => _randomTrackCommand ?? (_randomTrackCommand = new DelegateCommand(() => {
                var track = GetRandomObject(TracksManager.Instance, SelectedTrack?.Id,
                        string.IsNullOrWhiteSpace(SettingsHolder.Drive.QuickDriveRandomizeTrackFilter) ? null
                                : Filter.Create(TrackObjectBaseTester.Instance, SettingsHolder.Drive.QuickDriveRandomizeTrackFilter));
                SelectedTrack = track?.MultiLayouts?.RandomElementOrDefault() ?? track;
            }));

            public int TimeMultiplerMinimum => 0;
            public int TimeMultiplerMaximum => 3600;

            private int _timeMultiplier;

            public int TimeMultiplier {
                get => _timeMultiplier;
                set {
                    if (value == _timeMultiplier) return;
                    _timeMultiplier = value.Clamp(TimeMultiplerMinimum, TimeMultiplerMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }
            #endregion

            private readonly ISaveHelper _saveable;

            internal void SaveLater() {
                if (_saveable == null) return;
                if (_uiMode && _saveable.SaveLater()) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }

            private readonly string _carSkinId, _carSetupFilename, _weatherId;
            private readonly int? _forceTime;

            [CanBeNull]
            private readonly DiscordRichPresence _presence;

            private bool IsToLoadAssists() {
                return SettingsHolder.Drive.LoadAssistsWithQuickDrivePreset ^ (Keyboard.Modifiers == ModifierKeys.Control);
            }

            public bool CustomDateSupported { get; }

            internal ViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null, string carSetupFilename = null,
                    TrackObjectBase track = null, TrackSkinObject trackSkin = null, string weatherId = null, int? time = null, bool savePreset = false,
                    Uri mode = null, string serializedRaceGrid = null, DiscordRichPresence presence = null, bool forceAssistsLoading = false) {
                if (uiMode && SettingsHolder.Drive.ShowExtraComboBoxes) {
                    CarsManager.Instance.WrappersList.ItemPropertyChanged += OnListItemPropertyChanged;
                    CarsManager.Instance.WrappersList.WrappedValueChanged += OnListWrappedValueChanged;
                    CarsManager.Instance.EnsureLoadedAsync().Ignore();
                }

                CustomDateSupported = PatchHelper.IsFeatureSupported(PatchHelper.FeatureSpecificDate);

                _uiMode = uiMode;
                _carSkinId = carSkinId;
                _carSetupFilename = carSetupFilename;
                _weatherId = weatherId;
                // _serializedRaceGrid = serializedRaceGrid;
                _forceTime = time;
                _presence = presence;

                if (trackSkin != null) {
                    var mainLayout = TracksManager.Instance.GetById(trackSkin.TrackId);
                    mainLayout?.ForceSkinEnabled(trackSkin);
                    track = mainLayout;
                }

                UpdateTimeDiapason();
                WeatherManager.Instance.WrappersList.CollectionChanged += OnWeatherListChanged;
                WeatherManager.Instance.WrappersList.WrappedValueChanged += OnWeatherListChanged;
                WeatherManager.Instance.WrappersList.ItemPropertyChanged += OnWeatherPropertyChanged;

                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    RealConditions = RealConditions,
                    IdealConditions = IdealConditions,
                    RealConditionsLocalWeather = RealConditionsLocalWeather,
                    RealConditionsTimezones = RealConditionsTimezones,
                    RealConditionsManualTime = RealConditionsManualTime,
                    RealConditionsManualWind = RealConditionsManualWind,
                    Mode = SelectedMode,
                    ModeData = SelectedModeViewModel?.ToSerializedString(),
                    CarId = SelectedCar?.Id,
                    TrackId = SelectedTrack?.IdWithLayout,
                    WeatherId = WeatherTypeWrapped.Serialize(SelectedWeather),
                    TrackPropertiesData = TrackState.ExportToPresetData(),
                    TrackPropertiesChanged = UserPresetsControl.IsChanged(TrackState.PresetableKey),
                    TrackPropertiesPresetFilename = UserPresetsControl.GetCurrentFilename(TrackState.PresetableKey),
                    AssistsData = AssistsViewModel.ExportToPresetData(),
                    AssistsChanged = UserPresetsControl.IsChanged(AssistsViewModel.PresetableKey),
                    AssistsPresetFilename = UserPresetsControl.GetCurrentFilename(AssistsViewModel.PresetableKey),
                    Temperature = Temperature,
                    Time = Time,
                    TimeMultipler = TimeMultiplier,
                    WindSpeedMin = WindSpeedMin,
                    WindSpeedMax = WindSpeedMax,
                    WindDirection = WindDirection,
                    RandomWindSpeed = RandomWindSpeed,
                    RandomWindDirection = RandomWindDirection,
                    RandomTemperature = RandomTemperature,
                    RandomTime = RandomTime,
                    CustomRoadTemperature = CustomRoadTemperature,
                    CustomRoadTemperatureValue = _customRoadTemperatureValue,
                    UseSpecificDate = UseSpecificDate,
                    SpecificDateValue = SpecificDateValue
                }, o => {
                    TimeMultiplier = o.TimeMultipler;

                    RealConditions = _weatherId == null && o.RealConditions;
                    IdealConditions = _weatherId == null && o.IdealConditions;
                    RealConditionsTimezones = o.RealConditionsTimezones ?? true;
                    RealConditionsLocalWeather = o.RealConditionsLocalWeather ?? false;
                    RealConditionsManualTime = o.RealConditionsManualTime ?? false;
                    RealConditionsManualWind = o.RealConditionsManualWind ?? false;

                    Temperature = o.Temperature;
                    Time = o.Time;
                    WindSpeedMin = o.WindSpeedMin;
                    WindSpeedMax = o.WindSpeedMax;
                    WindDirection = o.WindDirection.RoundToInt();
                    RandomWindSpeed = o.RandomWindSpeed;
                    RandomWindDirection = o.RandomWindDirection;

                    UseSpecificDate = o.UseSpecificDate;
                    SpecificDateValue = o.SpecificDateValue ?? DateTime.Now;

                    RandomTemperature = o.RandomTemperature;
                    RandomTime = o.RandomTime;
                    CustomRoadTemperature = o.CustomRoadTemperature;
                    _customRoadTemperatureValue = o.CustomRoadTemperatureValue;
                    OnPropertyChanged(nameof(CustomRoadTemperatureValue));

                    if (RealConditions) {
                        UpdateConditions();
                    }

                    try {
                        _skipLoading = o.ModeData != null;
                        if (o.Mode != null && o.Mode.OriginalString.Contains('_')) SelectedMode = o.Mode;
                        if (o.ModeData != null) SelectedModeViewModel?.FromSerializedString(o.ModeData);
                    } finally {
                        _skipLoading = false;
                    }

                    if (o.CarId != null) SelectedCar = CarsManager.Instance.GetById(o.CarId) ?? SelectedCar;
                    if (o.TrackId != null) SelectedTrack = TracksManager.Instance.GetLayoutById(o.TrackId) ?? SelectedTrack;

                    SelectedWeather = WeatherTypeWrapped.Deserialize(_weatherId ?? o.WeatherId) ?? SelectedWeather;

                    if (forceAssistsLoading || IsToLoadAssists()) {
                        LoadPreset(AssistsViewModel, o.AssistsPresetFilename, o.AssistsData, o.AssistsChanged);
                    }

                    if (!LoadPreset(TrackState, o.TrackPropertiesPresetFilename, o.TrackPropertiesData, o.TrackPropertiesChanged)
                            && o.ObsTrackPropertiesPreset != null
                            && !UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory,
                                    o.ObsTrackPropertiesPreset)) {
                        TrackState.ImportFromPresetData(o.ObsTrackPropertiesPreset);
                    }

                    bool LoadPreset(IUserPresetable p, string filename, string data, bool isChanged) {
                        try {
                            if (filename != null) {
                                if (data != null) {
                                    if (!UserPresetsControl.LoadPreset(p.PresetableKey, filename, data, isChanged)) {
                                        p.ImportFromPresetData(data);
                                    }
                                    return true;
                                }

                                if (PresetsManager.Instance.HasBuiltInPreset(p.PresetableCategory, filename)) {
                                    if (!UserPresetsControl.LoadPreset(p.PresetableKey, filename)) {
                                        p.ImportFromPresetData(
                                                PresetsManager.Instance.GetBuiltInPreset(p.PresetableCategory, filename).ReadData());
                                    }
                                    return true;
                                }

                                if (File.Exists(filename)) {
                                    if (!UserPresetsControl.LoadPreset(p.PresetableKey, filename)) {
                                        p.ImportFromPresetData(File.ReadAllText(filename));
                                    }
                                    return true;
                                }
                            }

                            if (data != null) {
                                if (!UserPresetsControl.LoadSerializedPreset(p.PresetableKey, data)) {
                                    p.ImportFromPresetData(data);
                                }
                                return true;
                            }
                        } catch (Exception e) {
                            NonfatalError.NotifyBackground($"Can’t load settings for {p.PresetableKey}: {e}");
                        }
                        return false;
                    }
                }, () => {
                    RealConditions = false;
                    IdealConditions = false;
                    RealConditionsTimezones = true;
                    RealConditionsManualTime = false;
                    RealConditionsManualWind = false;
                    RealConditionsLocalWeather = false;

                    SelectedMode = ModeRace;
                    SelectedCar = CarsManager.Instance.GetDefault();
                    SelectedTrack = TracksManager.Instance.GetDefault();
                    SelectedWeather = WeatherManager.Instance.GetDefault();

                    UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, @"Green");

                    if (forceAssistsLoading || IsToLoadAssists()) {
                        UserPresetsControl.LoadBuiltInPreset(AssistsViewModel.PresetableKey, AssistsViewModel.PresetableCategory,
                                ControlsStrings.AssistsPreset_Pro);
                    }

                    Temperature = 12.0;
                    Time = 12 * 60 * 60;
                    TimeMultiplier = 1;
                    WindSpeedMin = 10;
                    WindSpeedMax = 10;
                    WindDirection = 0;
                    RandomWindSpeed = false;
                    RandomWindDirection = false;

                    UseSpecificDate = false;
                    SpecificDateValue = DateTime.Now;

                    RandomTemperature = false;
                    RandomTime = false;
                    CustomRoadTemperature = false;
                    _customRoadTemperatureValue = null;
                    OnPropertyChanged(nameof(CustomRoadTemperatureValue));
                });

                if (string.IsNullOrEmpty(serializedPreset)) {
                    _saveable.LoadOrReset();
                } else {
                    _saveable.Reset();
                    if (savePreset) {
                        _saveable.FromSerializedString(serializedPreset);
                    } else {
                        _saveable.FromSerializedStringWithoutSaving(serializedPreset);
                    }
                }

                if (WindSpeedMin == 10d && WindSpeedMax == 10d) {
                    FancyHints.DoubleSlider.Trigger();
                }

                if (carObject != null) {
                    SelectedCar = carObject;
                    // TODO: Skin?
                }

                if (track != null) {
                    SelectedTrack = track;
                }

                if (mode != null) {
                    SelectedMode = mode;
                }

                if (time.HasValue) {
                    Time = time.Value;
                }

                if (serializedRaceGrid != null) {
                    (SelectedModeViewModel as IRaceGridModeViewModel)?.SetRaceGridData(serializedRaceGrid);
                }

                UpdateConditions();

                //UpdateHierarchicalWeatherList().Ignore();
                //WeakEventManager<IBaseAcObjectObservableCollection, EventArgs>.AddHandler(WeatherManager.Instance.WrappersList,
                //       nameof(IBaseAcObjectObservableCollection.CollectionReady), OnWeatherListUpdated);

                FancyHints.MoreDriveAssists.Trigger(TimeSpan.FromSeconds(1d));

                var stored = Stored.Get("windDirectionInDegrees");
                if (stored.Value != null) {
                    FancyHints.DegreesWind.MarkAsUnnecessary();
                } else {
                    FancyHints.DegreesWind.Trigger(TimeSpan.FromSeconds(1.5d));
                    stored.SubscribeWeak((o, e) => {
                        if (e.PropertyName == nameof(StoredValue.Value)) {
                            FancyHints.DegreesWind.MarkAsUnnecessary();
                        }
                    });
                }
            }

            #region Presets
            bool IUserPresetable.CanBeSaved => true;
            PresetsCategory IUserPresetable.PresetableCategory => new PresetsCategory(PresetableKeyValue);
            string IUserPresetable.PresetableKey => PresetableKeyValue;

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }
            #endregion

            public AssistsViewModel AssistsViewModel => AssistsViewModel.Instance;

            private ICommand _changeCarCommand;

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new DelegateCommand(() => {
                var dialog = new SelectCarDialog(SelectedCar).ApplyDefault(SelectedModeViewModel?.GetDefaultCarFilter());
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                var car = dialog.SelectedCar;
                car.SelectedSkin = dialog.SelectedSkin;

                SelectedCar = car;
                FancyHints.DragForContentSection.Trigger();
            }));

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                // var extra = this.SelectedModeViewModel.GetSpecificTrackSelectionPage();
                SelectedTrack = SelectTrackDialog.Show(SelectedTrack, SelectedModeViewModel?.GetDefaultTrackFilter());
            }));

            private DelegateCommand _manageCarSetupsCommand;

            public DelegateCommand ManageCarSetupsCommand => _manageCarSetupsCommand ?? (_manageCarSetupsCommand = new DelegateCommand(() => {
                if (SelectedCar != null) {
                    CarSetupsListPage.Open(SelectedCar);
                }
            }));

            private DelegateCommand _manageCarCommand;

            public DelegateCommand ManageCarCommand => _manageCarCommand ?? (_manageCarCommand =
                    new DelegateCommand(() => CarsListPage.Show(SelectedCar)));

            private DelegateCommand _manageTrackCommand;

            public DelegateCommand ManageTrackCommand => _manageTrackCommand ?? (_manageTrackCommand =
                    new DelegateCommand(() => TracksListPage.Show(SelectedTrack)));

            private QuickDriveModeViewModel _selectedModeViewModel;

            private CommandBase _goCommand;

            public ICommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(Go, () => SelectedCar != null && SelectedTrack != null && SelectedModeViewModel != null));

            private enum TrackDoesNotFitRespond {
                Cancel,
                Go,
                FixAndGo
            }

            private static TrackDoesNotFitRespond ShowTrackDoesNotFitMessage(string message) {
                switch (MessageDialog.Show(
                        $"Most likely, track won’t work with selected mode: {message.ToSentenceMember()}. Are you sure you want to continue?",
                        ToolsStrings.Common_Warning, new MessageDialogButton {
                            [MessageBoxResult.Yes] = UiStrings.Yes,
                            [MessageBoxResult.OK] = AppStrings.Drive_Quick_YesAndFixIt,
                            [MessageBoxResult.No] = UiStrings.No,
                        }, "incompatibleTrack")) {
                    case MessageBoxResult.Yes:
                        return TrackDoesNotFitRespond.Go;
                    case MessageBoxResult.OK:
                        return TrackDoesNotFitRespond.FixAndGo;
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                    case MessageBoxResult.No:
                        return TrackDoesNotFitRespond.Cancel;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private static TrackDoesNotFitRespond ShowCarOrTrackDoesNotFitMessage(string car, string track) {
                var subject = car == null ? "Track" : track == null ? "Car" : "Car and track";
                var dlg = new ModernDialog {
                    Title = ToolsStrings.Common_Warning,
                    Content = new ScrollViewer {
                        Content = new SelectableBbCodeBlock {
                            Text = $"{subject} won’t work with selected mode. Are you sure you want to continue?",
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    },
                    MinHeight = 0,
                    MinWidth = 0,
                    MaxHeight = 480,
                    MaxWidth = 640
                };

                dlg.Buttons = new[] {
                    dlg.YesButton,
                    dlg.NoButton
                };

                dlg.ShowDialog();

                switch (dlg.MessageBoxResult) {
                    case MessageBoxResult.Yes:
                        return TrackDoesNotFitRespond.Go;
                    case MessageBoxResult.OK:
                        return TrackDoesNotFitRespond.FixAndGo;
                    case MessageBoxResult.None:
                    case MessageBoxResult.Cancel:
                    case MessageBoxResult.No:
                        return TrackDoesNotFitRespond.Cancel;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            internal async Task Go() {
                var selectedCar = SelectedCar;
                var selectedMode = SelectedModeViewModel;
                if (selectedCar == null || selectedMode == null) return;

                if (SettingsHolder.Drive.QuickDriveCheckTrack) {
                    var doesNotFitCar = selectedMode.CarDoesNotFit;
                    var doesNotFitTrack = selectedMode.TrackDoesNotFit;
                    if (doesNotFitTrack != null && doesNotFitCar == null && doesNotFitTrack.Item2 != QuickDriveModeViewModel.EmptyTrackAction) {
                        var respond = ShowTrackDoesNotFitMessage(doesNotFitTrack.Item1);
                        if (respond == TrackDoesNotFitRespond.Cancel) return;

                        if (respond == TrackDoesNotFitRespond.FixAndGo) {
                            doesNotFitTrack.Item2(SelectedTrack);
                        }
                    } else if (doesNotFitCar != null || doesNotFitTrack != null) {
                        if (ShowCarOrTrackDoesNotFitMessage(doesNotFitCar?.Item1, doesNotFitTrack?.Item1) == TrackDoesNotFitRespond.Cancel) {
                            return;
                        }
                    }
                }

                double temperature;
                var weather = SelectedWeatherObject;
                int time;

                if (weather == null) {
                    temperature = RandomTemperature ? GetRandomTemperature() : Temperature;
                    time = _forceTime ?? (RandomTime ? GetRandomTime() : Time);
                    weather = GetRandomWeather(time, temperature);
                } else {
                    temperature = RandomTemperature ? GetRandomTemperature() : Temperature;
                    time = _forceTime ?? (RandomTime ? GetRandomTime() : Time);
                }

                // Null for procedural weathers to be able to specify custom weather coefficient
                var roadTemperature = CustomRoadTemperature ? CustomRoadTemperatureValue : (double?)null;
                var track = SelectedTrack ?? TracksManager.Instance.GetDefault();
                if (track == null) return;

                try {
                    await selectedMode.Drive(new Game.BasicProperties {
                        CarId = selectedCar.Id,
                        CarSkinId = _carSkinId ?? selectedCar.SelectedSkin?.Id,
                        CarSetupFilename = _carSetupFilename,
                        TrackId = track.Id,
                        TrackConfigurationId = track.LayoutId
                    }, AssistsViewModel.ToGameProperties(), new Game.ConditionProperties {
                        AmbientTemperature = temperature,
                        RoadTemperature = roadTemperature,
                        SunAngle = Game.ConditionProperties.GetSunAngle(time),
                        TimeMultipler = TimeMultiplier,
                        CloudSpeed = 0.2,
                        WeatherName = weather?.Id,
                        WindDirectionDeg = RandomWindDirection ? MathUtils.Random(0, 360) : WindDirection,
                        WindSpeedMin = RandomWindSpeed ? 2 : WindSpeedMin,
                        WindSpeedMax = RandomWindSpeed ? 40 : WindSpeedMax,
                    }, TrackState.ToProperties(), ExportToPresetData(), new object[] {
                        new WeatherSpecificDate(UseSpecificDate, SpecificDateValue),
                        new WeatherDetails(RealWeather, SelectedWeather as WeatherTypeWrapped, 
                                WeatherFxControllerData.Instance.Items.FirstOrDefault(x => x.IsSelectedAsBase)?.Id),
                        TrackState.WeatherDefined ? new CustomTrackState(Path.Combine(weather?.Location ?? ".", "track_state.ini")) : null
                    });
                } finally {
                    _goCommand?.RaiseCanExecuteChanged();
                }

                Logging.Here();
            }

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                var data = ExportToPresetData();
                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.QuickDrivePreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(PresetableKeyValue)), null,
                        data);
            }

            private void OnSelectedUpdated() {
                SelectedModeViewModel?.OnSelectedUpdated(SelectedCar, SelectedTrack);
            }

            [CanBeNull]
            public QuickDriveModeViewModel SelectedModeViewModel {
                get => _selectedModeViewModel;
                set {
                    if (Equals(value, _selectedModeViewModel)) return;
                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed -= OnModeModelChanged;
                    }

                    _selectedModeViewModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));

                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed += OnModeModelChanged;
                        OnSelectedUpdated();
                    }
                }
            }

            private void OnModeModelChanged(object sender, EventArgs e) {
                SaveLater();
            }

            internal bool Run() {
                if (GoCommand.CanExecute(null)) {
                    GoCommand.Execute(null);
                    return true;
                }

                return false;
            }
        }

        public static void LoadPreset(string presetFilename) {
            UserPresetsControl.LoadPreset(PresetableKeyValue, presetFilename);
            NavigateToPage();
        }

        public static void LoadSerializedPreset(string serializedPreset) {
            if (!UserPresetsControl.LoadSerializedPreset(PresetableKeyValue, serializedPreset)) {
                ValuesStorage.Set(KeySaveable, serializedPreset);
            }

            NavigateToPage();
        }

        private static readonly Uri QuickDriveUri = new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative);

        public static void NavigateToPage() {
            if (IsActive()) return;
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(QuickDriveUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(QuickDriveUri);
                    break;
            }
        }

        public static bool IsActive() {
            return (Application.Current?.MainWindow as MainWindow)?.CurrentSource == QuickDriveUri;
        }

        private static string _selectNextSerializedPreset;
        private static CarObject _selectNextCar;
        private static string _selectNextCarSkinId;
        private static TrackObjectBase _selectNextTrack;
        private static TrackSkinObject _selectNextTrackSkin;
        private static WeatherObject _selectNextWeather;
        private static Uri _selectNextMode;
        private static string _selectNextSerializedRaceGrid;
        private static bool _selectNextForceAssistsLoading;
        private static int? _selectNextTime;

        public static async Task Activate(bool setupRace,
                CarObject car = null, string carSkinId = null, string carSetupFilename = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                string weatherId = null, int? time = null,
                string serializedPreset = null, string presetFilename = null,
                Uri mode = null, string serializedRaceGrid = null,
                bool forceAssistsLoading = false) {
            if (setupRace || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                || !await RunAsync(car, carSkinId, carSetupFilename, track, trackSkin, weatherId, time, serializedPreset, presetFilename, mode, serializedRaceGrid, forceAssistsLoading)) {
                Show(car, carSkinId, carSetupFilename, track, trackSkin, weatherId, time, serializedPreset, presetFilename, mode, serializedRaceGrid, forceAssistsLoading);
            }
        }

        public static void Show(
                CarObject car = null, string carSkinId = null, string carSetupFilename = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                string weatherId = null, int? time = null,
                string serializedPreset = null, string presetFilename = null,
                Uri mode = null, string serializedRaceGrid = null,
                bool forceAssistsLoading = false) {
            var weather = weatherId == null ? null : WeatherManager.Instance.GetById(weatherId);

            if (_current != null && _current.TryGetTarget(out var current) && current.IsLoaded) {
                var vm = current.Model;
                vm.SelectedCar = car ?? vm.SelectedCar;
                if (vm.SelectedCar != null && carSkinId != null) {
                    vm.SelectedCar.SelectedSkin = vm.SelectedCar.GetSkinById(carSkinId);
                }

                vm.SelectedTrack = track ?? vm.SelectedTrack;
                if (time.HasValue) {
                    vm.RandomTime = false;
                    vm.IdealConditions = false;
                    vm.Time = time.Value;
                }

                if (weather != null) {
                    vm.SelectedWeather = weather;
                }
            }

            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null || mainWindow is MainWindow) {
                _selectNextSerializedPreset = serializedPreset ?? (presetFilename != null ? File.ReadAllText(presetFilename) : null);
                _selectNextForceAssistsLoading = forceAssistsLoading;

                _selectNextCar = car;
                _selectNextCarSkinId = carSkinId;
                // TODO: carSetupId?

                _selectNextTrack = track;
                _selectNextTrackSkin = trackSkin;
                _selectNextWeather = weather;
                _selectNextMode = mode;
                _selectNextSerializedRaceGrid = serializedRaceGrid;
                _selectNextTime = time;

                NavigateToPage();
            }
        }

        public static async Task<bool> RunAsync(
                CarObject car = null, string carSkinId = null, string carSetupFilename = null,
                TrackObjectBase track = null, TrackSkinObject trackSkin = null,
                string weatherId = null, int? time = null,
                string serializedPreset = null, string presetFilename = null,
                Uri mode = null, string serializedRaceGrid = null,
                bool forceAssistsLoading = false) {
            if (serializedPreset == null) {
                serializedPreset = presetFilename != null ? File.ReadAllText(presetFilename) : string.Empty;
            }

            var model = new ViewModel(serializedPreset, false, car, carSkinId, carSetupFilename, track, trackSkin, weatherId, time,
                    mode: mode, serializedRaceGrid: serializedRaceGrid, forceAssistsLoading: forceAssistsLoading);
            if (!model.GoCommand.CanExecute(null)) {
                Logging.Warning(AppStrings.Drive_Quick_CantStartTheRace);
                return false;
            }

            Logging.Write(AppStrings.Drive_Quick_StartingTheRace);
            await model.Go();
            return true;
        }

        public static IContentLoader ContentLoader { get; } = new ImmediateContentLoader();

        private void OnCarBlockDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            Model.SelectedCar = carObject ?? raceGridEntry.Car;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void OnTrackBlockDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(TrackObjectBase.DraggableFormat) is TrackObjectBase trackObject) {
                Model.SelectedTrack = trackObject;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnCarContextMenu(object sender, ContextMenuButtonEventArgs e) {
            var selectedCar = Model.SelectedCar;
            if (selectedCar == null) return;

            var menu = new ContextMenu()
                    .AddItem(AppStrings.QuickDrive_ChangeCar_Tooltip, Model.ChangeCarCommand)
                    .AddItem(AppStrings.Drive_Quick_ChangeCarToRandom, Model.RandomCarCommand, @"Ctrl+Alt+1")
                    .AddItem(AppStrings.Drive_Quick_ChangeSkinToRandom, Model.RandomCarSkinCommand, @"Ctrl+Alt+R")
                    .AddItem(AppStrings.Drive_Quick_RandomizeEverything, Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"))
                    .AddSeparator();
            ContextMenus.ContextMenusProvider.SetCarObjectMenu(menu, selectedCar, null);
            e.Menu = menu;
        }

        private void OnTrackContextMenu(object sender, ContextMenuButtonEventArgs e) {
            if (Model.SelectedTrack == null) return;
            var menu = new ContextMenu()
                    .AddItem(AppStrings.QuickDrive_ChangeTrack_Tooltip, Model.ChangeTrackCommand)
                    .AddItem(AppStrings.Drive_Quick_ChangeTrackToRandom, Model.RandomTrackCommand, @"Ctrl+Alt+2")
                    .AddItem(AppStrings.Drive_Quick_RandomizeEverything, Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"))
                    .AddSeparator();
            ContextMenus.ContextMenusProvider.SetTrackObjectMenu(menu, Model.SelectedTrack);
            e.Menu = menu;
        }

        private void OnCarBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            Model.ChangeCarCommand.Execute(null);
        }

        private void OnTrackBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            Model.ChangeTrackCommand.Execute(null);
        }

        private void OnConditionsContextMenu(object sender, ContextMenuButtonEventArgs e) {
            if (Model.RealConditions) {
                e.Menu = (TryFindResource(@"RealConditionsContextMenu") as ContextMenu)?
                        .AddSeparator()
                        .AddItem(AppStrings.Drive_Quick_SetRandomTime, Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem(AppStrings.Drive_Quick_RandomizeEverything, Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            } else {
                e.Menu = new ContextMenu()
                        .AddItem(AppStrings.Drive_Quick_SetRandomTime, Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem(AppStrings.Drive_Quick_SetRandomWeather, Model.RandomWeatherCommand, @"Ctrl+Alt+4")
                        .AddItem(AppStrings.Drive_Quick_SetRandomTemperature, Model.RandomTemperatureCommand, @"Ctrl+Alt+5")
                        .AddItem(AppStrings.Drive_Quick_RandomizeEverything, Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            }
        }

        private void OnShowroomButtonClick(object sender, RoutedEventArgs e) {
            var selectedCar = Model.SelectedCar;
            if (selectedCar == null) return;
            CarBlock.OnShowroomButtonClick(Model.SelectedCar, selectedCar.SelectedSkin);
        }

        private void OnShowroomContextMenu(object sender, MouseButtonEventArgs e) {
            var selectedCar = Model.SelectedCar;
            if (selectedCar == null) return;
            CarBlock.OnShowroomContextMenu(Model.SelectedCar, selectedCar.SelectedSkin);
        }

        public static DelegateCommand<AcObjectNew> OpenInQuickDrive { get; }

        static QuickDrive() {
            OpenInQuickDrive = new DelegateCommand<AcObjectNew>(o => {
                switch (o) {
                    case CarObject car:
                        Show(car);
                        break;
                    case CarSkinObject skin:
                        Show(CarsManager.Instance.GetById(skin.CarId), skin.Id);
                        break;
                    case TrackObjectBase track:
                        Show(track: track);
                        break;
                    case TrackSkinObject skin:
                        Show(track: TracksManager.Instance.GetById(skin.TrackId), trackSkin: skin);
                        break;
                    case WeatherObject weather:
                        Show(weatherId: weather.Id);
                        break;
                }
            });
        }

        private void OnCarTrackSeparatorDrag(object sender, DragDeltaEventArgs e) {
            CarCell.Width = (CarCell.Width + e.HorizontalChange / 2).Clamp(80d, (ActualWidth - 400d) / 2d);
            TrackCell.Width = CarCell.Width;
            ValuesStorage.Set(".qd.rz.w", TrackCell.Width);
        }

        private void OnTopRowSeparatorDrag(object sender, DragDeltaEventArgs e) {
            CarCellBase.Height = (CarCellBase.Height + e.VerticalChange).Clamp(120d, 400d);
            TrackCellBase.Height = CarCellBase.Height;
            ValuesStorage.Set(".qd.rz.h", CarCellBase.Height);
        }
    }
}