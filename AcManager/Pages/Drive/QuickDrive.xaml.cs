using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Lists;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive : ILoadableContent {
        public const string PresetableKeyValue = "Quick Drive";
        private const string KeySaveable = "__QuickDrive_Main";

        private ViewModel Model => (ViewModel)DataContext;

        public Task LoadAsync(CancellationToken cancellationToken) {
            return WeatherManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            WeatherManager.Instance.EnsureLoaded();
        }

        private static WeakReference<QuickDrive> _current;

        public void Initialize() {
            OnSizeChanged(null, null);

            DataContext = new ViewModel(null, true, _selectNextCar, _selectNextCarSkinId, _selectNextTrack, mode: _selectNextMode);
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(Model.TrackState, nameof(INotifyPropertyChanged.PropertyChanged),
                    OnTrackStateChanged);
            this.OnActualUnload(() => {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(Model.TrackState, nameof(INotifyPropertyChanged.PropertyChanged),
                        OnTrackStateChanged);
            });

            _current = new WeakReference<QuickDrive>(this);

            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),

#if DEBUG
                new InputBinding(new AsyncCommand(() => {
                    return LapTimesManager.Instance.AddEntry(Model.SelectedCar.Id, Model.SelectedTrack.IdWithLayout, DateTime.Now, TimeSpan.FromSeconds(MathUtils.Random(10d, 20d)));
                }), new KeyGesture(Key.T, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)),
#endif

                new InputBinding(Model.RandomizeCommand, new KeyGesture(Key.R, ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarSkinCommand, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomCarCommand, new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTrackCommand, new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTimeCommand, new KeyGesture(Key.D3, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomWeatherCommand, new KeyGesture(Key.D4, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(Model.RandomTemperatureCommand, new KeyGesture(Key.D5, ModifierKeys.Control | ModifierKeys.Alt)),
            });

            _selectNextCar = null;
            _selectNextCarSkinId = null;
            _selectNextTrack = null;
            _selectNextMode = null;
        }

        private void OnTrackStateChanged(object sender, PropertyChangedEventArgs e) {
            Model.SaveLater();
        }

        private DispatcherTimer _realConditionsTimer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
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
            var c = ModeTab.Frame.Content as IQuickDriveModeControl;
            if (c != null) {
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

            public string TrackPropertiesPresetFilename, TrackPropertiesData;

            [JsonProperty(@"tpc")]
            public bool TrackPropertiesChanged;

            // Obsolete
            [JsonProperty(@"TrackPropertiesPreset")]
#pragma warning disable 649
            public string ObsTrackPropertiesPreset;
#pragma warning restore 649

            [JsonProperty(@"rcTimezones")]
            public bool? RealConditionsTimezones;

            [JsonProperty(@"rcManTime")]
            public bool? RealConditionsManualTime;

            [JsonProperty(@"rcLw")]
            public bool? RealConditionsLocalWeather;

            [JsonProperty(@"crt")]
            public bool CustomRoadTemperature;

            [JsonProperty(@"crtv")]
            public double? CustomRoadTemperatureValue;
        }

        public partial class ViewModel : NotifyPropertyChanged, IUserPresetable {
            private readonly bool _uiMode;

            #region Notifieable Stuff
            private Uri _selectedMode;
            private CarObject _selectedCar;
            private TrackObjectBase _selectedTrack;

            private bool _skipLoading;

            public Uri SelectedMode {
                get { return _selectedMode; }
                set {
                    if (Equals(value, _selectedMode)) return;
                    _selectedMode = value;
                    OnPropertyChanged();
                    SaveLater();

                    switch (value.OriginalString) {
                        case "/Pages/Drive/QuickDrive_Drift.xaml":
                            SelectedModeViewModel = new QuickDrive_Drift.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Hotlap.xaml":
                            SelectedModeViewModel = new QuickDrive_Hotlap.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Practice.xaml":
                            SelectedModeViewModel = new QuickDrive_Practice.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Race.xaml":
                            SelectedModeViewModel = new QuickDrive_Race.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Trackday.xaml":
                            SelectedModeViewModel = new QuickDrive_Trackday.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Weekend.xaml":
                            SelectedModeViewModel = new QuickDrive_Weekend.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_TimeAttack.xaml":
                            SelectedModeViewModel = new QuickDrive_TimeAttack.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_Drag.xaml":
                            SelectedModeViewModel = new QuickDrive_Drag.ViewModel(!_skipLoading);
                            break;

                        default:
                            Logging.Warning("Not supported mode: " + value);
                            SelectedMode = new Uri("/Pages/Drive/QuickDrive_Practice.xaml", UriKind.Relative);
                            break;
                    }
                }
            }

            private AsyncCommand _randomizeCommand;

            public AsyncCommand RandomizeCommand => _randomizeCommand ?? (_randomizeCommand = new AsyncCommand(async () => {
                RandomCarCommand.Execute();
                await Task.Delay(100);
                RandomTrackCommand.Execute();
                await Task.Delay(100);
                RandomTimeCommand.Execute();
                await Task.Delay(100);
                RandomWeatherCommand.Execute();
                await Task.Delay(100);
                RandomTemperatureCommand.Execute();
            }));

            public CarObject SelectedCar {
                get { return _selectedCar; }
                set {
                    if (Equals(value, _selectedCar)) return;
                    _selectedCar = value;
                    // _selectedCar?.LoadSkins();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();
                }
            }

            private static T GetRandomObject<T>(BaseAcManager<T> manager, string currentId) where T : AcObjectNew {
                var id = manager.WrappersList.Where(x => x.Value.Enabled).Select(x => x.Id).ApartFrom(currentId).RandomElementOrDefault() ?? currentId;
                return manager.GetById(id) ?? manager.GetDefault();
            }

            private DelegateCommand _randomCarCommand;

            public DelegateCommand RandomCarCommand => _randomCarCommand ?? (_randomCarCommand = new DelegateCommand(() => {
                SelectedCar = GetRandomObject(CarsManager.Instance, SelectedCar?.Id);
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id);
                }
            }));

            private DelegateCommand _randomCarSkinCommand;

            public DelegateCommand RandomCarSkinCommand => _randomCarSkinCommand ?? (_randomCarSkinCommand = new DelegateCommand(() => {
                if (SelectedCar != null) {
                    SelectedCar.SelectedSkin = GetRandomObject(SelectedCar.SkinsManager, SelectedCar.SelectedSkin?.Id);
                }
            }));

            public TrackStateViewModel TrackState => TrackStateViewModel.Instance;

            public TrackObjectBase SelectedTrack {
                get { return _selectedTrack; }
                set {
                    if (Equals(value, _selectedTrack)) return;
                    _selectedTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();
                    SaveLater();
                    TrackUpdated();
                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                }
            }

            private DelegateCommand _randomTrackCommand;

            public DelegateCommand RandomTrackCommand => _randomTrackCommand ?? (_randomTrackCommand = new DelegateCommand(() => {
                var track = GetRandomObject(TracksManager.Instance, SelectedTrack.Id);
                SelectedTrack = track.MultiLayouts?.RandomElementOrDefault() ?? track;
            }));

            public int TimeMultiplerMinimum => 0;

            public int TimeMultiplerMaximum => 360;

            public int TimeMultiplerMaximumLimited => 60;

            private int _timeMultipler;

            public int TimeMultipler {
                get { return _timeMultipler; }
                set {
                    if (value == _timeMultipler) return;
                    _timeMultipler = value.Clamp(TimeMultiplerMinimum, TimeMultiplerMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }
            #endregion

            private readonly ISaveHelper _saveable;

            internal void SaveLater() {
                if (!_uiMode) return;

                if (_saveable.SaveLater()) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }

            private readonly string _carSetupId, _weatherId;
            private readonly int? _forceTime;

            internal ViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null,
                    TrackObjectBase trackObject = null, string carSetupId = null, string weatherId = null, int? time = null, bool savePreset = false,
                    Uri mode = null) {
                _uiMode = uiMode;
                _carSetupId = carSetupId;
                _weatherId = weatherId;
                _forceTime = time;

                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    RealConditions = RealConditions,
                    RealConditionsLocalWeather = RealConditionsLocalWeather,
                    RealConditionsTimezones = RealConditionsTimezones,
                    RealConditionsManualTime = RealConditionsManualTime,

                    Mode = SelectedMode,
                    ModeData = SelectedModeViewModel?.ToSerializedString(),

                    CarId = SelectedCar?.Id,
                    TrackId = SelectedTrack?.IdWithLayout,
                    WeatherId = SelectedWeather?.Id,
                    
                    TrackPropertiesData = TrackState.ExportToPresetData(),
                    TrackPropertiesChanged = UserPresetsControl.IsChanged(TrackState.PresetableKey),
                    TrackPropertiesPresetFilename = UserPresetsControl.GetCurrentFilename(TrackState.PresetableKey),

                    Temperature = Temperature,
                    Time = Time,
                    TimeMultipler = TimeMultipler,

                    CustomRoadTemperature = CustomRoadTemperatureEnabled,
                    CustomRoadTemperatureValue = _customRoadTemperatureValue
                }, o => {
                    TimeMultipler = o.TimeMultipler;

                    RealConditions = _weatherId == null && o.RealConditions;
                    RealConditionsTimezones = o.RealConditionsTimezones ?? true;
                    RealConditionsLocalWeather = o.RealConditionsLocalWeather ?? false;
                    RealConditionsManualTime = o.RealConditionsManualTime ?? false;

                    Temperature = o.Temperature;
                    Time = o.Time;

                    CustomRoadTemperatureEnabled = o.CustomRoadTemperature;
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
                    if (_weatherId != null) {
                        SelectedWeather = WeatherManager.Instance.GetById(_weatherId);
                    } else if (o.WeatherId != null) {
                        SelectedWeather = WeatherManager.Instance.GetById(o.WeatherId) ?? SelectedWeather;
                    }

                    if (o.TrackPropertiesPresetFilename != null && o.TrackPropertiesData != null) {
                        UserPresetsControl.LoadPreset(TrackState.PresetableKey, o.TrackPropertiesPresetFilename, o.TrackPropertiesData, o.TrackPropertiesChanged);
                    } else if (o.TrackPropertiesPresetFilename != null &&
                            (PresetsManager.Instance.HasBuiltInPreset(TrackStateViewModelBase.PresetableCategory, o.TrackPropertiesPresetFilename) ||
                                    File.Exists(o.TrackPropertiesPresetFilename))) {
                        UserPresetsControl.LoadPreset(TrackState.PresetableKey, o.TrackPropertiesPresetFilename);
                    } else if (o.TrackPropertiesData != null) {
                        UserPresetsControl.LoadSerializedPreset(TrackState.PresetableKey, o.TrackPropertiesData);
                    } else if (o.ObsTrackPropertiesPreset != null) {
                        UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, o.ObsTrackPropertiesPreset);
                    }
                }, () => {
                    RealConditions = false;
                    RealConditionsTimezones = true;
                    RealConditionsManualTime = false;
                    RealConditionsLocalWeather = false;

                    SelectedMode = new Uri("/Pages/Drive/QuickDrive_Race.xaml", UriKind.Relative);
                    SelectedCar = CarsManager.Instance.GetDefault();
                    SelectedTrack = TracksManager.Instance.GetDefault();
                    SelectedWeather = WeatherManager.Instance.GetDefault();

                    UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, "Green");

                    Temperature = 12.0;
                    Time = 12 * 60 * 60;
                    TimeMultipler = 1;

                    CustomRoadTemperatureEnabled = false;
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

                if (carObject != null) {
                    SelectedCar = carObject;
                    // TODO: skin?
                }

                if (trackObject != null) {
                    SelectedTrack = trackObject;
                }

                if (mode != null) {
                    SelectedMode = mode;
                }

                UpdateConditions();
            }

            #region Presets
            bool IUserPresetable.CanBeSaved => true;

            string IUserPresetable.PresetableCategory => PresetableKeyValue;

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
                var dialog = new SelectCarDialog(SelectedCar);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                var car = dialog.SelectedCar;
                car.SelectedSkin = dialog.SelectedSkin;

                SelectedCar = car;
            }));

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                // var extra = this.SelectedModeViewModel.GetSpecificTrackSelectionPage();
                SelectedTrack = SelectTrackDialog.Show(SelectedTrack);
            }));

            private QuickDriveModeViewModel _selectedModeViewModel;

            private CommandBase _goCommand;

            public ICommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(Go, () => SelectedCar != null && SelectedTrack != null && SelectedModeViewModel != null));

            private enum TrackDoesNotFitRespond {
                Cancel, Go, FixAndGo
            }

            private static TrackDoesNotFitRespond ShowTrackDoesNotFitMessage(string message) {
                var dlg = new ModernDialog {
                    Title = ToolsStrings.Common_Warning,
                    Content = new ScrollViewer {
                        Content = new SelectableBbCodeBlock {
                            BbCode = $"Most likely, track won’t work with selected mode: {message.ToSentenceMember()}. Are you sure you want to continue?",
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
                    dlg.CreateCloseDialogButton("Yes, And Fix It", false, false, MessageBoxResult.OK),
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
                var selectedMode = SelectedModeViewModel;
                if (selectedMode == null) return;

                if (SettingsHolder.Drive.QuickDriveCheckTrack) {
                    var doesNotFit = selectedMode.TrackDoesNotFit;
                    if (doesNotFit != null) {
                        var respond = ShowTrackDoesNotFitMessage(doesNotFit.Item1);
                        if (respond == TrackDoesNotFitRespond.Cancel) return;

                        if (respond == TrackDoesNotFitRespond.FixAndGo) {
                            doesNotFit.Item2(SelectedTrack);
                        }
                    }
                }

                try {
                    await selectedMode.Drive(new Game.BasicProperties {
                        CarId = SelectedCar.Id,
                        CarSkinId = SelectedCar.SelectedSkin?.Id,
                        CarSetupId = _carSetupId,
                        TrackId = SelectedTrack.Id,
                        TrackConfigurationId = SelectedTrack.LayoutId
                    }, AssistsViewModel.ToGameProperties(), new Game.ConditionProperties {
                        AmbientTemperature = Temperature,
                        RoadTemperature = CustomRoadTemperatureEnabled ? CustomRoadTemperatureValue : RoadTemperature,

                        SunAngle = Game.ConditionProperties.GetSunAngle(_forceTime ?? Time),
                        TimeMultipler = TimeMultipler,
                        CloudSpeed = 0.2,

                        WeatherName = SelectedWeather?.Id
                    }, TrackState.ToProperties());
                } finally {
                    _goCommand?.RaiseCanExecuteChanged();
                }
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
                get { return _selectedModeViewModel; }
                set {
                    if (Equals(value, _selectedModeViewModel)) return;
                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed -= SelectedModeViewModel_Changed;
                    }

                    _selectedModeViewModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));

                    if (_selectedModeViewModel != null) {
                        _selectedModeViewModel.Changed += SelectedModeViewModel_Changed;
                        OnSelectedUpdated();
                    }
                }
            }

            private void SelectedModeViewModel_Changed(object sender, EventArgs e) {
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

        public static bool Run(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, string carSetupId = null, Uri mode = null) {
            return new ViewModel(string.Empty, false, car, carSkinId, track, carSetupId, mode: mode).Run();
        }

        public static bool RunHotlap(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, string carSetupId = null) {
            return Run(car, carSkinId, track, carSetupId, new Uri("/Pages/Drive/QuickDrive_Hotlap.xaml", UriKind.Relative));
        }

        public static async Task<bool> RunAsync(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, string carSetupId = null,
                string weatherId = null, int? time = null) {
            var model = new ViewModel(string.Empty, false, car, carSkinId, track, carSetupId, weatherId, time);
            if (!model.GoCommand.CanExecute(null)) return false;
            await model.Go();
            return true;
        }

        public static bool RunPreset(string presetFilename, CarObject car = null, string carSkinId = null, TrackObjectBase track = null,
                string carSetupId = null) {
            return new ViewModel(File.ReadAllText(presetFilename), false, car, carSkinId, track, carSetupId).Run();
        }

        public static bool RunSerializedPreset(string preset) {
            return new ViewModel(preset, false).Run();
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

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative));
        }

        private static CarObject _selectNextCar;
        private static string _selectNextCarSkinId;
        private static TrackObjectBase _selectNextTrack;
        private static Uri _selectNextMode;

        public static void Show(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, Uri mode = null) {
            QuickDrive current;
            if (_current != null && _current.TryGetTarget(out current) && current.IsLoaded) {
                var vm = current.Model;
                vm.SelectedCar = car ?? vm.SelectedCar;
                vm.SelectedTrack = track ?? vm.SelectedTrack;
                if (vm.SelectedCar != null && carSkinId != null) {
                    vm.SelectedCar.SelectedSkin = vm.SelectedCar.GetSkinById(carSkinId);
                }
            }

            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car;
            _selectNextCarSkinId = carSkinId;
            _selectNextTrack = track;
            _selectNextMode = mode;

            NavigateToPage();
        }

        public static void ShowHotlap(CarObject car = null, string carSkinId = null, TrackObjectBase track = null) {
            Show(car, carSkinId, track, new Uri("/Pages/Drive/QuickDrive_Hotlap.xaml", UriKind.Relative));
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
            var trackObject = e.Data.GetData(TrackObjectBase.DraggableFormat) as TrackObjectBase;

            if (trackObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            Model.SelectedTrack = trackObject;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void SelectedCarContextMenuButton_OnClick(object sender, ContextMenuButtonEventArgs e) {
            e.Menu = new ContextMenu()
                    .AddItem("Change car to random", Model.RandomCarCommand, @"Ctrl+Alt+1")
                    .AddItem("Change skin to random", Model.RandomCarSkinCommand, @"Ctrl+Alt+R")
                    .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"))
                    .AddSeparator()
                    .AddItem("Open car in Content tab", () => {
                        CarsListPage.Show(Model.SelectedCar, Model.SelectedCar.SelectedSkin?.Id);
                    });
        }

        private void SelectedTrackContextMenuButton_OnClick(object sender, ContextMenuButtonEventArgs e) {
            e.Menu = new ContextMenu()
                    .AddItem("Change track to random", Model.RandomTrackCommand, @"Ctrl+Alt+2")
                    .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"))
                    .AddSeparator()
                    .AddItem("Open track in Content tab", () => {
                        TracksListPage.Show(Model.SelectedTrack);
                    }, isEnabled: AppKeyHolder.IsAllRight);
        }

        private void OnCarBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            Model.ChangeCarCommand.Execute(null);
        }

        private void OnTrackBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            Model.ChangeTrackCommand.Execute(null);
        }

        private void ConditionsContextMenuButton_OnClick(object sender, ContextMenuButtonEventArgs e) {
            if (Model.RealConditions) {
                e.Menu = (TryFindResource(@"RealConditionsContextMenu") as ContextMenu)?
                        .AddSeparator()
                        .AddItem("Set random time", Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            } else {
                e.Menu = new ContextMenu()
                        .AddItem("Set random time", Model.RandomTimeCommand, @"Ctrl+Alt+3")
                        .AddItem("Set random weather", Model.RandomWeatherCommand, @"Ctrl+Alt+4")
                        .AddItem("Set random temperature", Model.RandomTemperatureCommand, @"Ctrl+Alt+5")
                        .AddItem("Randomize everything", Model.RandomizeCommand, @"Alt+R", iconData: (Geometry)TryFindResource(@"ShuffleIconData"));
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            LeftPanel.Width = 180 + ((ActualWidth - 800) / 2d).Clamp(0, 60);
        }
    }
}
