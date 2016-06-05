using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive {
        public const string UserPresetableKeyValue = "Quick Drive";
        private const string KeySaveable = "__QuickDrive_Main";

        private readonly QuickDriveViewModel _model;

        public QuickDrive() {
            InitializeComponent();
            DataContext = _model = new QuickDriveViewModel(null, true, _selectNextCar, _selectNextCarSkinId, _selectNextTrack);

            _selectNextCar = null;
            _selectNextCarSkinId = null;
            _selectNextTrack = null;
        }

        private DispatcherTimer _realConditionsTimer;

        private void QuickDrive_Loaded(object sender, RoutedEventArgs e) {
            _realConditionsTimer = new DispatcherTimer();
            _realConditionsTimer.Tick += (o, args) => {
                if (_model.RealConditions) {
                    _model.TryToSetRealConditions();
                }
            };
            _realConditionsTimer.Interval = new TimeSpan(0, 0, 60);
            _realConditionsTimer.Start();
        }

        private void QuickDrive_Unloaded(object sender, RoutedEventArgs e) {
            _realConditionsTimer.Stop();
        }

        private void ModeTab_OnFrameNavigated(object sender, NavigationEventArgs e) {
            var c = ModeTab.Frame.Content as IQuickDriveModeControl;
            if (c != null) {
                c.Model = _model.SelectedModeViewModel;
            }

            // _model.SelectedModeViewModel = (ModeTab.Frame.Content as IQuickDriveModeControl)?.Model;
        }

        private void AssistsMore_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            new AssistsDialog(_model.AssistsViewModel).ShowDialog();
        }

        public class QuickDriveViewModel : NotifyPropertyChanged, IUserPresetable {
            private readonly bool _uiMode;

            #region Notifieable Stuff
            private Uri _selectedMode;
            private CarObject _selectedCar;
            private TrackBaseObject _selectedTrack;
            private WeatherObject _selectedWeather;
            private bool _realConditions,
                _isTimeClamped, _isTemperatureClamped, _isWeatherNotSupported,
                _realConditionsTimezones, _realConditionsLighting;
            private double _temperature;
            private int _time;

            public Uri SelectedMode {
                get { return _selectedMode; }
                set {
                    if (Equals(value, _selectedMode)) return;
                    _selectedMode = value;
                    OnPropertyChanged();
                    SaveLater();

                    // if (_uiMode) return;
                    switch (value.ToString()) {
                        case "/Pages/Drive/QuickDrive_Drift.xaml":
                            SelectedModeViewModel = new QuickDrive_Drift.QuickDrive_DriftViewModel();
                            break;

                        case "/Pages/Drive/QuickDrive_Hotlap.xaml":
                            SelectedModeViewModel = new QuickDrive_Hotlap.QuickDrive_HotlapViewModel();
                            break;

                        case "/Pages/Drive/QuickDrive_Practice.xaml":
                            SelectedModeViewModel = new QuickDrive_Practice.QuickDrive_PracticeViewModel();
                            break;

                        case "/Pages/Drive/QuickDrive_Race.xaml":
                            SelectedModeViewModel = new QuickDrive_Race.QuickDrive_RaceViewModel();
                            break;

                        case "/Pages/Drive/QuickDrive_Weekend.xaml":
                            SelectedModeViewModel = new QuickDrive_Weekend.QuickDrive_WeekendViewModel();
                            break;

                        case "/Pages/Drive/QuickDrive_TimeAttack.xaml":
                            SelectedModeViewModel = new QuickDrive_TimeAttack.QuickDrive_TimeAttackViewModel();
                            break;

                        default:
                            Logging.Warning("[QuickDrive] Not supported mode: " + value);
                            SelectedModeViewModel = null;
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

            public Game.TrackPropertiesPreset SelectedTrackPropertiesPreset {
                get { return _selectedTrackPropertiesPreset; }
                set {
                    if (Equals(value, _selectedTrackPropertiesPreset)) return;
                    _selectedTrackPropertiesPreset = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public TrackBaseObject SelectedTrack {
                get { return _selectedTrack; }
                set {
                    if (Equals(value, _selectedTrack)) return;
                    _selectedTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    OnSelectedUpdated();

                    _selectedTrackGeoTags = null;
                    _selectedTrackTimeZone = null;
                    RealWeather = null;
                    SaveLater();

                    if (RealConditions) {
                        TryToSetRealConditions();
                    }

                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                }
            }

            public WeatherObject SelectedWeather {
                get { return _selectedWeather; }
                set {
                    if (Equals(value, _selectedWeather)) return;
                    _selectedWeather = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (!RealConditions) {
                        SaveLater();
                    }
                }
            }

            public bool RealConditions {
                get { return _realConditions; }
                set {
                    if (value == _realConditions) return;
                    _realConditions = value;

                    if (value) {
                        TryToSetRealConditions();
                    } else {
                        IsTimeClamped = IsTemperatureClamped =
                            IsWeatherNotSupported = false;
                        RealWeather = null;
                    }

                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public bool IsTimeClamped {
                get { return _isTimeClamped; }
                set {
                    if (value == _isTimeClamped) return;
                    _isTimeClamped = value;
                    OnPropertyChanged();
                }
            }

            public bool IsTemperatureClamped {
                get { return _isTemperatureClamped; }
                set {
                    if (value == _isTemperatureClamped) return;
                    _isTemperatureClamped = value;
                    OnPropertyChanged();
                }
            }

            public bool IsWeatherNotSupported {
                get { return _isWeatherNotSupported; }
                set {
                    if (value == _isWeatherNotSupported) return;
                    _isWeatherNotSupported = value;
                    OnPropertyChanged();
                }
            }

            public bool RealConditionsTimezones {
                get { return _realConditionsTimezones; }
                set {
                    if (value == _realConditionsTimezones) return;
                    _realConditionsTimezones = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public bool RealConditionsLighting {
                get { return _realConditionsLighting; }
                set {
                    if (value == _realConditionsLighting) return;
                    _realConditionsLighting = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            // default limit: 10/36
            public double TemperatureMinimum => 0.0;
            public double TemperatureMaximum => 36.0;
            public double Temperature {
                get { return _temperature; }
                set {
                    value = value.Round(0.5);
                    if (Equals(value, _temperature)) return;
                    _temperature = value.Clamp(TemperatureMinimum, TemperatureMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (!RealConditions) {
                        SaveLater();
                    }
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeather?.TemperatureCoefficient ?? 0.0);

            public int TimeMinimum => 8 * 60 * 60;
            public int TimeMaximum => 18 * 60 * 60;
            public int Time {
                get { return _time; }
                set {
                    if (value == _time) return;
                    _time = value.Clamp(TimeMinimum, TimeMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (!RealConditions) {
                        SaveLater();
                    }
                }
            }

            public string DisplayTime {
                get { return $"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}"; }
                set {
                    int time;
                    if (!FlexibleParser.TryParseTime(value, out time)) return;
                    Time = time;
                }
            }

            public int TimeMultiplerMinimum => 0;

            public int TimeMultiplerMaximum => 360;

            public int TimeMultiplerMaximumLimited => 60;

            public int TimeMultipler {
                get { return _timeMultipler; }
                set {
                    if (value == _timeMultipler) return;
                    _timeMultipler = value.Clamp(TimeMultiplerMinimum, TimeMultiplerMaximum);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            public WeatherDescription RealWeather {
                get { return _realWeather; }
                set {
                    if (Equals(value, _realWeather)) return;
                    _realWeather = value;
                    OnPropertyChanged();
                }
            }

            public AcLoadedOnlyCollection<WeatherObject> WeatherList => WeatherManager.Instance.LoadedOnlyCollection;
            #endregion

            private GeoTagsEntry _selectedTrackGeoTags;
            private static readonly GeoTagsEntry InvalidGeoTagsEntry = new GeoTagsEntry("", "");
            private TimeZoneInfo _selectedTrackTimeZone;
            private static readonly TimeZoneInfo InvalidTimeZoneInfo = TimeZoneInfo.CreateCustomTimeZone("_", TimeSpan.Zero,
                                                                                                         "", "");

            private class SaveableData {
                public Uri Mode;
                public string ModeData, CarId, TrackId, WeatherId, TrackPropertiesPreset;
                public bool RealConditions, RealConditionsTimezones, RealConditionsLighting;
                public double Temperature;
                public int Time, TimeMultipler;
            }

            private readonly ISaveHelper _saveable;

            private void SaveLater() {
                if (!_uiMode) return;

                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            internal QuickDriveViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null, 
                    TrackBaseObject trackObject = null, bool savePreset = false) {
                _uiMode = uiMode;

                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    RealConditions = RealConditions,
                    RealConditionsTimezones = RealConditionsTimezones,
                    RealConditionsLighting = RealConditionsLighting,

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
                    RealConditions = o.RealConditions;
                    RealConditionsTimezones = o.RealConditionsTimezones;
                    RealConditionsLighting = o.RealConditionsLighting;

                    if (o.Mode != null) SelectedMode = o.Mode ?? SelectedMode;
                    if (o.ModeData != null) SelectedModeViewModel?.FromSerializedString(o.ModeData);

                    if (o.CarId != null) SelectedCar = CarsManager.Instance.GetById(o.CarId) ?? SelectedCar;
                    if (o.TrackId != null) SelectedTrack = TracksManager.Instance.GetLayoutById(o.TrackId) ?? SelectedTrack;
                    if (o.WeatherId != null) SelectedWeather = WeatherManager.Instance.GetById(o.WeatherId) ?? SelectedWeather;

                    if (o.TrackPropertiesPreset != null) {
                        SelectedTrackPropertiesPreset =
                                Game.DefaultTrackPropertiesPresets.FirstOrDefault(x => x.Name == o.TrackPropertiesPreset) ?? SelectedTrackPropertiesPreset;
                    }

                    Temperature = o.Temperature;
                    Time = o.Time;
                    TimeMultipler = o.TimeMultipler;
                }, () => {
                    RealConditionsTimezones = false;
                    RealConditionsLighting = false;
                    RealConditions = false;

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
                    _saveable.Initialize();
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
            }

            #region Presets
            bool IUserPresetable.CanBeSaved => true;

            string IUserPresetable.UserPresetableKey => UserPresetableKeyValue;

            public string ExportToUserPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromUserPresetData(string data) {
                _saveable.FromSerializedString(data);
            }
            #endregion

            public AssistsViewModel AssistsViewModel => AssistsViewModel.Instance;

            private bool _realConditionsInProcess;

            public async void TryToSetRealConditions() {
                if (_realConditionsInProcess || !RealConditions) return;
                _realConditionsInProcess = true;

                if (_selectedTrackGeoTags == null) {
                    var geoTags = SelectedTrack.GeoTags;
                    if (geoTags == null || geoTags.IsEmptyOrInvalid) {
                        geoTags = await Task.Run(() => TracksLocator.TryToLocate(SelectedTrack));
                        if (!RealConditions) {
                            _realConditionsInProcess = false;
                            return;
                        }

                        if (geoTags == null) {
                            // TODO: Informing
                            geoTags = InvalidGeoTagsEntry;
                        }
                    }

                    _selectedTrackGeoTags = geoTags;
                }

                TryToSetRealTime();
                TryToSetRealWeather();

                _realConditionsInProcess = false;
            }

            #region Real Time
            private const int SecondsPerDay = 24 * 60 * 60;
            private bool _realTimeInProcess;
            private Game.TrackPropertiesPreset _selectedTrackPropertiesPreset;

            private async void TryToSetRealTime() {
                if (_realTimeInProcess || !RealConditions) return;
                _realTimeInProcess = true;

                var now = DateTime.Now;
                var time = now.Hour * 60 * 60 + now.Minute * 60 + now.Second;

                if (_selectedTrackGeoTags == null || _selectedTrackGeoTags == InvalidGeoTagsEntry) {
                    TryToSetTime(time);
                    return;
                }

                if (_selectedTrackTimeZone == null) {
                    var timeZone = await Task.Run(() => TimeZoneDeterminer.TryToDetermine(_selectedTrackGeoTags));
                    if (!RealConditions) {
                        _realTimeInProcess = false;
                        return;
                    }

                    if (timeZone == null) {
                        // TODO: Informing
                        timeZone = InvalidTimeZoneInfo;
                    }

                    _selectedTrackTimeZone = timeZone;
                }

                if (_selectedTrackTimeZone == null || ReferenceEquals(_selectedTrackTimeZone, InvalidTimeZoneInfo)) {
                    TryToSetTime(time);
                    return;
                }

                time += (int)(_selectedTrackTimeZone.BaseUtcOffset.TotalSeconds - TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds);
                time = (time + SecondsPerDay) % SecondsPerDay;

                TryToSetTime(time);
            }

            private void TryToSetTime(int value) {
                var clamped = value.Clamp(TimeMinimum, TimeMaximum);
                IsTimeClamped = clamped != value;
                Time = clamped;
                _realTimeInProcess = false;
            }
            #endregion

            #region Real Weather
            private bool _realWeatherInProcess;
            private WeatherDescription _realWeather;
            private int _timeMultipler;

            private async void TryToSetRealWeather() {
                if (_realWeatherInProcess || !RealConditions) return;

                if (_selectedTrackGeoTags == null || _selectedTrackGeoTags == InvalidGeoTagsEntry) {
                    return;
                }

                _realWeatherInProcess = true;

                var weather = await Task.Run(() => WeatherProvider.TryToGetWeather(_selectedTrackGeoTags));
                if (!RealConditions) {
                    _realWeatherInProcess = true;
                    return;
                }

                if (weather != null) {
                    RealWeather = weather;
                    TryToSetTemperature(weather.Temperature);
                    await TryToSetWeatherType(weather.Type);
                }

                _realWeatherInProcess = false;
            }

            private void TryToSetTemperature(double value) {
                var clamped = value.Clamp(TemperatureMinimum, TemperatureMaximum);
                IsTemperatureClamped = value < TemperatureMinimum || value > TemperatureMaximum;
                Temperature = clamped;
            }

            private bool _waitingForWeatherList;

            private async Task TryToSetWeatherType(WeatherDescription.WeatherType type) {
                if (_waitingForWeatherList) return;

                _waitingForWeatherList = true;
                await WeatherManager.Instance.EnsureLoadedAsync();
                _waitingForWeatherList = false;

                try {
                    var closest = WeatherDescription.FindClosestWeather(from w in WeatherManager.Instance.LoadedOnly
                                                                        where w.WeatherType.HasValue
                                                                        select w.WeatherType.Value, type);
                    if (closest == null) {
                        IsWeatherNotSupported = true;
                    } else {
                        SelectedWeather = WeatherManager.Instance.LoadedOnly.Where(x => x.WeatherType == closest).RandomElement();
                    }
                } catch (Exception e) {
                    IsWeatherNotSupported = true;
                    Logging.Warning("[QUICKDRIVE] FindClosestWeather exception: " + e);
                }
            }
            #endregion

            private ICommand _changeCarCommand;

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new RelayCommand(o => {
                var dialog = new SelectCarDialog(SelectedCar);
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;

                SelectedCar = dialog.SelectedCar;
                SelectedCar.SelectedSkin = dialog.SelectedSkin;
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
                    await selectedMode.Drive(SelectedCar, SelectedTrack, AssistsViewModel.GameProperties, new Game.ConditionProperties {
                        AmbientTemperature = Temperature,
                        RoadTemperature = RoadTemperature,

                        SunAngle = Game.ConditionProperties.GetSunAngle(Time),
                        TimeMultipler = TimeMultipler,
                        CloudSpeed = 0.2,

                        WeatherName = SelectedWeather?.Id
                    }, SelectedTrackPropertiesPreset.Properties);
                } finally {
                    GoCommand.OnCanExecuteChanged();
                }
            }

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share(object o) {
                await SharingUiHelper.ShareAsync(SharingHelper.EntryType.QuickDrivePreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(UserPresetableKeyValue)), null,
                        ExportToUserPresetData());
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
                Changed?.Invoke(this, EventArgs.Empty);
            }

            internal bool Run() {
                if (GoCommand.CanExecute(null)) {
                    GoCommand.Execute(null);
                    return true;
                }

                return false;
            }
        }

        public static bool Run(CarObject car = null, string carSkinId = null, TrackBaseObject track = null) {
            return new QuickDriveViewModel(string.Empty, false, car, carSkinId, track).Run();
        }

        public static async Task<bool> RunAsync(CarObject car = null, string carSkinId = null, TrackBaseObject track = null) {
            var model = new QuickDriveViewModel(string.Empty, false, car, carSkinId, track);
            if (!model.GoCommand.CanExecute(null)) return false;
            await model.Go();
            return true;
        }

        public static bool RunPreset(string presetFilename, CarObject car = null, string carSkinId = null, TrackBaseObject track = null) {
            return new QuickDriveViewModel(File.ReadAllText(presetFilename), false, car, carSkinId, track).Run();
        }

        public static bool RunSerializedPreset(string preset) {
            return new QuickDriveViewModel(preset, false).Run();
        }

        public static void LoadPreset(string presetFilename) {
            UserPresetsControl.LoadPreset(UserPresetableKeyValue, presetFilename);
            NavigateToPage();
        }

        public static void LoadSerializedPreset(string serializedPreset) {
            if (!UserPresetsControl.LoadSerializedPreset(UserPresetableKeyValue, serializedPreset)) {
                ValuesStorage.Set(KeySaveable, serializedPreset);
            }

            NavigateToPage();
        }

        private static void NavigateToPage() {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;
            mainWindow.NavigateTo(new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative));
        }

        private static CarObject _selectNextCar;
        private static string _selectNextCarSkinId;
        private static TrackBaseObject _selectNextTrack;

        public static void Show(CarObject car = null, string carSkinId = null, TrackBaseObject track = null) {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car;
            _selectNextCarSkinId = carSkinId;
            _selectNextTrack = track;

            NavigateToPage();
        }

        public static IContentLoader ContentLoader { get; } = new ImmediateContentLoader();
    }

    public interface IQuickDriveModeControl {
        QuickDriveModeViewModel Model { get; set; }
    }

    public abstract class QuickDriveModeViewModel : NotifyPropertyChanged {
        protected ISaveHelper Saveable { set; get; }

        public event EventHandler Changed;

        protected void SaveLater() {
            Saveable.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        public abstract Task Drive(CarObject selectedCar, TrackBaseObject selectedTrack,
            Game.AssistsProperties assistsProperties,
            Game.ConditionProperties conditionProperties, Game.TrackProperties trackProperties);

        protected async Task StartAsync(Game.StartProperties properties) {
            await GameWrapper.StartAsync(properties);
        }

        public virtual void OnSelectedUpdated(CarObject selectedCar, TrackBaseObject selectedTrack) {
        }

        public string ToSerializedString() {
            return Saveable.ToSerializedString();
        }

        public void FromSerializedString(string data) {
            Saveable.FromSerializedString(data);
        }
    }
}
