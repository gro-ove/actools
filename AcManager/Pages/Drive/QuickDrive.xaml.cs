using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Navigation;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive : ILoadableContent {
        public static bool OptionTestMode = false;

        public static double TimeMinimum { get; } = 8d * 60 * 60;

        public static double TimeMaximum { get; } = 18d * 60 * 60;

        public static double TemperatureMinimum { get; } = 0d;

        public static double TemperatureMaximum { get; } = 36d;

        public const string PresetableKeyValue = "Quick Drive";
        private const string KeySaveable = "__QuickDrive_Main";

        private ViewModel Model => (ViewModel)DataContext;

        public Task LoadAsync(CancellationToken cancellationToken) {
            return WeatherManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            WeatherManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            DataContext = new ViewModel(null, true, _selectNextCar, _selectNextCarSkinId, _selectNextTrack);
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.GoCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });

            _selectNextCar = null;
            _selectNextCarSkinId = null;
            _selectNextTrack = null;
            
            if (OptionTestMode) {
                ModeTab.Links.Add(new Link {
                    DisplayName = "Test",
                    IsNew = true,
                    Source = new Uri("/Pages/Drive/QuickDrive_GridTest.xaml", UriKind.Relative)
                });
            }
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
            _realConditionsTimer.Interval = new TimeSpan(0, 0, 60);
            _realConditionsTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_realConditionsTimer == null) return;
            _realConditionsTimer.Stop();
            _realConditionsTimer = null;
        }

        private void ModeTab_OnFrameNavigated(object sender, NavigationEventArgs e) {
            var c = ModeTab.Frame.Content as IQuickDriveModeControl;
            if (c != null) {
                c.Model = Model.SelectedModeViewModel;
            }

            // _model.SelectedModeViewModel = (ModeTab.Frame.Content as IQuickDriveModeControl)?.Model;
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(Model.AssistsViewModel).ShowDialog();
        }

        private class SaveableData {
            public Uri Mode;
            public string ModeData, CarId, TrackId, WeatherId, TrackPropertiesPreset;
            public bool RealConditions;
            public double Temperature;
            public int Time, TimeMultipler;

            [JsonProperty(@"rcTimezones")]
            public bool? RealConditionsTimezones;

            [JsonProperty(@"rcManTime")]
            public bool? RealConditionsManualTime;

            [JsonProperty(@"rcLw")]
            public bool? RealConditionsLocalWeather;
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

                        case "/Pages/Drive/QuickDrive_GridTest.xaml":
                            SelectedModeViewModel = new QuickDrive_GridTest.ViewModel(!_skipLoading);
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

                        default:
                            Logging.Warning("Not supported mode: " + value);
                            SelectedMode = new Uri("/Pages/Drive/QuickDrive_Practice.xaml", UriKind.Relative);
                            break;
                    }
                }
            }

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

            public BindingList<Game.TrackPropertiesPreset> TrackPropertiesPresets => Game.DefaultTrackPropertiesPresets;

            private Game.TrackPropertiesPreset _selectedTrackPropertiesPreset;

            public Game.TrackPropertiesPreset SelectedTrackPropertiesPreset {
                get { return _selectedTrackPropertiesPreset; }
                set {
                    if (Equals(value, _selectedTrackPropertiesPreset)) return;
                    _selectedTrackPropertiesPreset = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

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

            private void SaveLater() {
                if (!_uiMode) return;

                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            private readonly string _carSetupId, _weatherId;
            private readonly int? _forceTime;

            internal ViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null,
                    TrackObjectBase trackObject = null, string carSetupId = null, string weatherId = null, int? time = null, bool savePreset = false) {
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
                    TrackPropertiesPreset = SelectedTrackPropertiesPreset.Name,

                    Temperature = Temperature,
                    Time = Time,
                    TimeMultipler = TimeMultipler,
                }, o => {
                    Temperature = o.Temperature;
                    Time = o.Time;
                    TimeMultipler = o.TimeMultipler;

                    if (_weatherId == null && o.RealConditions) {
                        RealConditionsLocalWeather = o.RealConditionsLocalWeather ?? RealConditionsLocalWeather;
                        RealConditionsTimezones = o.RealConditionsTimezones ?? RealConditionsTimezones;
                        RealConditionsManualTime = o.RealConditionsManualTime ?? RealConditionsManualTime;
                        RealConditions = true;
                    } else {
                        RealConditions = false;
                        RealConditionsLocalWeather = false;
                        RealConditionsTimezones = false;
                        RealConditionsManualTime = false;
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

                    if (o.TrackPropertiesPreset != null) {
                        SelectedTrackPropertiesPreset =
                                Game.DefaultTrackPropertiesPresets.FirstOrDefault(x => x.Name == o.TrackPropertiesPreset) ?? SelectedTrackPropertiesPreset;
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
                    SelectedTrackPropertiesPreset = Game.GetDefaultTrackPropertiesPreset();

                    Temperature = 12.0;
                    Time = 12 * 60 * 60;
                    TimeMultipler = 1;
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
                
                UpdateConditions();
            }

            #region Presets
            bool IUserPresetable.CanBeSaved => true;

            string IUserPresetable.PresetableCategory => PresetableKeyValue;

            string IUserPresetable.PresetableKey => PresetableKeyValue;

            string IUserPresetable.DefaultPreset => null;

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

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new RelayCommand(o => {
                var dialog = new SelectCarDialog(SelectedCar);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                var car = dialog.SelectedCar;
                car.SelectedSkin = dialog.SelectedSkin;

                SelectedCar = car;
            }));

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new RelayCommand(o => {
                var dialog = new SelectTrackDialog(SelectedTrack);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null) return;

                SelectedTrack = dialog.Model.SelectedTrackConfiguration;
            }));

            private QuickDriveModeViewModel _selectedModeViewModel;

            private AsyncCommand _goCommand;

            public AsyncCommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(o => Go(), o => SelectedCar != null && SelectedTrack != null && SelectedModeViewModel != null));

            internal async Task Go() {
                GoCommand.OnCanExecuteChanged();

                var selectedMode = SelectedModeViewModel;
                if (selectedMode == null) return;

                try {
                    await selectedMode.Drive(new Game.BasicProperties {
                        CarId = SelectedCar.Id,
                        CarSkinId = SelectedCar.SelectedSkin?.Id,
                        CarSetupId = _carSetupId,
                        TrackId = SelectedTrack.Id,
                        TrackConfigurationId = SelectedTrack.LayoutId
                    }, AssistsViewModel.ToGameProperties(), new Game.ConditionProperties {
                        AmbientTemperature = Temperature,
                        RoadTemperature = RoadTemperature,

                        SunAngle = Game.ConditionProperties.GetSunAngle(_forceTime ?? Time),
                        TimeMultipler = TimeMultipler,
                        CloudSpeed = 0.2,

                        WeatherName = SelectedWeather?.Id
                    }, SelectedTrackPropertiesPreset.Properties);
                } finally {
                    GoCommand.OnCanExecuteChanged();
                }
            }

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new ProperAsyncCommand(Share));

            private async Task Share(object o) {
                await SharingUiHelper.ShareAsync(SharedEntryType.QuickDrivePreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(PresetableKeyValue)), null,
                        ExportToPresetData());
            }

            private void OnSelectedUpdated() {
                SelectedModeViewModel?.OnSelectedUpdated(SelectedCar, SelectedTrack);
            }

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
                    }
                    OnSelectedUpdated();
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

        public static bool Run(CarObject car = null, string carSkinId = null, TrackObjectBase track = null, string carSetupId = null) {
            return new ViewModel(string.Empty, false, car, carSkinId, track, carSetupId).Run();
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
            (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative));
        }

        private static CarObject _selectNextCar;
        private static string _selectNextCarSkinId;
        private static TrackObjectBase _selectNextTrack;

        public static void Show(CarObject car = null, string carSkinId = null, TrackObjectBase track = null) {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car;
            _selectNextCarSkinId = carSkinId;
            _selectNextTrack = track;

            NavigateToPage();
        }

        public static IContentLoader ContentLoader { get; } = new ImmediateContentLoader();

        private void MoreConditionsOptions_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            // TODO: Don’t reopen if menu just have closed
            ConditionsOptions.IsOpen = true;
        }

        private void Car_OnDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            Model.SelectedCar = carObject ?? raceGridEntry.Car;
            e.Effects = DragDropEffects.Copy;
        }
    }
}
