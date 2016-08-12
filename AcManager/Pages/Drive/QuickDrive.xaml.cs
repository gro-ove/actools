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
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive : ILoadableContent {
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
        }
        
        private DispatcherTimer _realConditionsTimer;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _realConditionsTimer = new DispatcherTimer();
            _realConditionsTimer.Tick += (o, args) => {
                if (Model.RealConditions) {
                    Model.TryToSetRealConditions();
                }
            };
            _realConditionsTimer.Interval = new TimeSpan(0, 0, 60);
            _realConditionsTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            _realConditionsTimer.Stop();
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

        public class ViewModel : NotifyPropertyChanged, IUserPresetable {
            private readonly bool _uiMode;

            #region Notifieable Stuff
            private Uri _selectedMode;
            private CarObject _selectedCar;
            private TrackBaseObject _selectedTrack;
            private WeatherObject _selectedWeather;
            private bool _realConditions,
                _isTimeClamped, _isTemperatureClamped, _isWeatherNotSupported,
                _realConditionsLocalWeather, _realConditionsManualTime, _realConditionsTimezones, _realConditionsLighting;
            private double _temperature;
            private int _time;

            private bool _skipLoading;

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

                        case "/Pages/Drive/QuickDrive_Weekend.xaml":
                            SelectedModeViewModel = new QuickDrive_Weekend.ViewModel(!_skipLoading);
                            break;

                        case "/Pages/Drive/QuickDrive_TimeAttack.xaml":
                            SelectedModeViewModel = new QuickDrive_TimeAttack.ViewModel(!_skipLoading);
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
                        ResetSettingRealConditions();
                        TryToSetRealConditions();
                    }

                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                }
            }

            private WeatherType _selectedWeatherType;

            public WeatherType SelectedWeatherType {
                get { return _selectedWeatherType; }
                set {
                    if (Equals(value, _selectedWeatherType)) return;
                    _selectedWeatherType = value;
                    OnPropertyChanged();

                    if (value != WeatherType.None) {
                        TryToSetWeather();
                    }
                }
            }

            [CanBeNull]
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
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ManualTime));
                    SaveLater();

                    if (value) {
                        TryToSetRealConditions();
                    } else {
                        IsTimeClamped = IsTemperatureClamped =
                            IsWeatherNotSupported = false;
                        RealWeather = null;
                    }
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

            public bool RealConditionsManualTime {
                get { return _realConditionsManualTime; }
                set {
                    if (value == _realConditionsManualTime) return;
                    _realConditionsManualTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ManualTime));
                    SaveLater();

                    if (!RealConditions) return;
                    if (value) {
                        IsTimeClamped = false;
                    } else {
                        TryToSetRealTime();
                    }
                }
            }

            public bool RealConditionsLocalWeather {
                get { return _realConditionsLocalWeather; }
                set {
                    if (value == _realConditionsLocalWeather) return;
                    _realConditionsLocalWeather = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (!RealConditions) return;
                    TryToSetRealConditions();
                }
            }

            private static GeoTagsEntry _localGeoTags;
            private static string _localAddress;

            private ICommand _switchLocalWeatherCommand;

            public ICommand SwitchLocalWeatherCommand => _switchLocalWeatherCommand ?? (_switchLocalWeatherCommand = new AsyncCommand(async o => {
                if (string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                    var entry = await Task.Run(() => IpGeoProvider.Get());
                    _localAddress = entry == null ? "" : $"{entry.City}, {entry.Country}";

                    var address = Prompt.Show("Where are you?", "Local Address", _localAddress);
                    if (string.IsNullOrWhiteSpace(address)) {
                        if (address != null) {
                            ModernDialog.ShowMessage("Value is required");
                        }

                        return;
                    }
                    
                    var tags = entry?.Location.Split(',').Select(x => x.AsDouble()).ToArray();
                    if (tags?.Length == 2) {
                        _localGeoTags = new GeoTagsEntry(tags[0], tags[1]);
                    }

                    SettingsHolder.Drive.LocalAddress = address;
                }

                RealConditionsLocalWeather = !RealConditionsLocalWeather;
            }));

            public bool ManualTime => !RealConditions || RealConditionsManualTime;

            public bool RealConditionsTimezones {
                get { return _realConditionsTimezones; }
                set {
                    if (value == _realConditionsTimezones) return;
                    _realConditionsTimezones = value;
                    OnPropertyChanged();
                    SaveLater();

                    TryToSetRealTime();
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
            public double Temperature {
                get { return _temperature; }
                set {
                    value = value.Round(0.5);
                    if (Equals(value, _temperature)) return;
                    _temperature = value.Clamp(TemperatureMinimum, SettingsHolder.Drive.QuickDriveExpandBounds ? TemperatureMaximum * 2 : TemperatureMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (!RealConditions) {
                        SaveLater();
                    }

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    }
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeather?.TemperatureCoefficient ?? 0.0);

            public int Time {
                get { return _time; }
                set {
                    if (value == _time) return;
                    _time = value.Clamp((int)TimeMinimum, (int)TimeMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (!RealConditions || RealConditionsManualTime) {
                        SaveLater();
                    }

                    if (RealConditions) {
                        TryToSetWeatherLater();
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

            [CanBeNull]
            public WeatherDescription RealWeather {
                get { return _realWeather; }
                set {
                    if (Equals(value, _realWeather)) return;
                    _realWeather = value;
                    OnPropertyChanged();
                    SelectedWeatherType = value?.Type ?? WeatherType.None;
                    if (value != null) {
                        TryToSetTemperature(value.Temperature);
                    }
                }
            }

            public AcEnabledOnlyCollection<WeatherObject> WeatherList => WeatherManager.Instance.EnabledOnlyCollection;
            #endregion

            private GeoTagsEntry _selectedTrackGeoTags;
            private TimeZoneInfo _selectedTrackTimeZone;
            private static readonly TimeZoneInfo InvalidTimeZoneInfo = TimeZoneInfo.CreateCustomTimeZone(@"_", TimeSpan.Zero, "", "");

            private class SaveableData {
                public Uri Mode;
                public string ModeData, CarId, TrackId, WeatherId, TrackPropertiesPreset;
                public bool RealConditions, RealConditionsLighting;
                public double Temperature;
                public int Time, TimeMultipler;

                [JsonProperty(@"rcTimezones")]
                public bool? RealConditionsTimezones;

                [JsonProperty(@"rcManTime")]
                public bool? RealConditionsManualTime;

                [JsonProperty(@"rcLw")]
                public bool? RealConditionsLocalWeather;
            }

            private readonly ISaveHelper _saveable;

            private void SaveLater() {
                if (!_uiMode) return;

                _saveable.SaveLater();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            private readonly string _carSetupId, _weatherId;
            private readonly int? _forceTime;

            internal ViewModel(string serializedPreset, bool uiMode, CarObject carObject = null, string carSkinId = null,
                    TrackBaseObject trackObject = null, string carSetupId = null, string weatherId = null, int? time = null, bool savePreset = false) {
                _uiMode = uiMode;
                _carSetupId = carSetupId;
                _weatherId = weatherId;
                _forceTime = time;

                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    RealConditions = RealConditions,
                    RealConditionsLocalWeather = RealConditionsLocalWeather,
                    RealConditionsTimezones = RealConditionsTimezones,
                    RealConditionsManualTime = RealConditionsManualTime,
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
                    RealConditionsLocalWeather = o.RealConditionsLocalWeather ?? RealConditionsLocalWeather;
                    RealConditionsTimezones = o.RealConditionsTimezones ?? RealConditionsTimezones;
                    RealConditionsManualTime = o.RealConditionsManualTime ?? RealConditionsManualTime;
                    RealConditionsLighting = o.RealConditionsLighting;
                    RealConditions = _weatherId == null && o.RealConditions;

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

                    Temperature = o.Temperature;
                    Time = o.Time;
                    TimeMultipler = o.TimeMultipler;
                }, () => {
                    RealConditionsTimezones = true;
                    RealConditionsManualTime = false;
                    RealConditionsLocalWeather = false;
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

            private void ResetSettingRealConditions() {
                if (!_realConditionsInProcess) return;
                _realConditionsInProcess = false;
                _realTimeInProcess = false;
                _realWeatherInProcess = false;
            }

            private bool _realConditionsInProcess;

            public async void TryToSetRealConditions() {
                if (_realConditionsInProcess || !RealConditions) return;
                _realConditionsInProcess = true;

                try {
                    if (_selectedTrackGeoTags == null && (!RealConditionsLocalWeather || RealConditionsTimezones)) {
                        var track = SelectedTrack;
                        var geoTags = track.GeoTags;
                        if (geoTags == null || geoTags.IsEmptyOrInvalid) {
                            geoTags = await Task.Run(() => TracksLocator.TryToLocate(track));
                            if (track != SelectedTrack) return;

                            Logging.Write($"[QuickDrive] {track.Name} geo tags: ({geoTags})");
                            if (!RealConditions) {
                                _realConditionsInProcess = false;
                                return;
                            }

                            if (geoTags == null) {
                                // TODO: Informing
                                geoTags = GeoTagsEntry.Invalid;
                            }
                        }

                        _selectedTrackGeoTags = geoTags;
                    }

                    if (RealConditionsLocalWeather && _localGeoTags == null && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                        var geoTags = await Task.Run(() => TracksLocator.TryToLocate(SettingsHolder.Drive.LocalAddress));
                        if (geoTags == null) {
                            // TODO: Informing
                            geoTags = GeoTagsEntry.Invalid;
                        }

                        _localGeoTags = geoTags;
                    }

                    TryToSetRealTime();
                    TryToSetRealWeather();
                } finally {
                    _realConditionsInProcess = false;
                }
            }

            #region Real Time
            private const int SecondsPerDay = 24 * 60 * 60;
            private bool _realTimeInProcess;
            private Game.TrackPropertiesPreset _selectedTrackPropertiesPreset;

            private async void TryToSetRealTime() {
                if (_realTimeInProcess || !RealConditions || RealConditionsManualTime) return;
                _realTimeInProcess = true;

                try {
                    var track = SelectedTrack;
                    var now = DateTime.Now;
                    var time = now.Hour * 60 * 60 + now.Minute * 60 + now.Second;

                    if (_selectedTrackGeoTags == null || _selectedTrackGeoTags == GeoTagsEntry.Invalid || !RealConditionsTimezones) {
                        TryToSetTime(time);
                        return;
                    }

                    if (_selectedTrackTimeZone == null) {
                        var timeZone = await Task.Run(() => TimeZoneDeterminer.TryToDetermine(_selectedTrackGeoTags));
                        if (track != SelectedTrack) return;

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
                } finally {
                    _realTimeInProcess = false;
                }
            }

            private void TryToSetTime(int value) {
                var clamped = value.Clamp((int)TimeMinimum, (int)TimeMaximum);
                IsTimeClamped = clamped != value;
                Time = clamped;
            }
            #endregion

            #region Real Weather
            private bool _realWeatherInProcess;
            private WeatherDescription _realWeather;
            private int _timeMultipler;

            private async void TryToSetRealWeather() {
                if (_realWeatherInProcess || !RealConditions) return;

                var tags = RealConditionsLocalWeather ? _localGeoTags : _selectedTrackGeoTags;
                if (tags == null || tags == GeoTagsEntry.Invalid) return;

                _realWeatherInProcess = true;

                try {
                    var track = SelectedTrack;

                    var weather = await Task.Run(() => WeatherProvider.TryToGetWeather(tags));
                    if (track != SelectedTrack) return;

                    if (!RealConditions) {
                        _realWeatherInProcess = true;
                        return;
                    }

                    if (weather != null) {
                        RealWeather = weather;
                    }
                } finally {
                    _realWeatherInProcess = false;
                }
            }

            private void TryToSetTemperature(double value) {
                var clamped = value.Clamp(TemperatureMinimum, TemperatureMaximum);
                IsTemperatureClamped = value < TemperatureMinimum || value > TemperatureMaximum;
                Temperature = clamped;
            }

            private bool _tryToSetWeatherLater;

            private async void TryToSetWeatherLater() {
                if (_tryToSetWeatherLater || _realTimeInProcess || _realWeatherInProcess || !RealConditions) return;
                _tryToSetWeatherLater = true;

                try {
                    await Task.Delay(500);
                    if (RealConditions) {
                        TryToSetWeather();
                    }
                } finally {
                    _tryToSetWeatherLater = false;
                }
            }

            private string _weatherCandidatesFootprint;

            private void TryToSetWeather() {
                if (SelectedWeatherType == WeatherType.None) return;

                try {
                    var candidates = WeatherManager.Instance.LoadedOnly.Where(x => x.Enabled && x.TemperatureDiapason?.DiapasonContains(Temperature) != false
                            && x.TimeDiapason?.TimeDiapasonContains(Time) != false).ToList();
                    var closest = WeatherDescription.FindClosestWeather(from w in candidates select w.Type, SelectedWeatherType);
                    if (closest == null) {
                        IsWeatherNotSupported = true;
                    } else {
                        candidates = candidates.Where(x => x.Type == closest).ToList();

                        var footprint = candidates.Select(x => x.Id).JoinToString(';');
                        if (footprint != _weatherCandidatesFootprint || !candidates.Contains(SelectedWeather)) {
                            SelectedWeather = candidates.RandomElement();
                            _weatherCandidatesFootprint = footprint;
                        }

                        IsWeatherNotSupported = false;
                    }
                } catch (Exception e) {
                    IsWeatherNotSupported = true;
                    Logging.Warning("[QuickDrive] TryToSetWeatherType(): " + e);
                }
            }
            #endregion

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
                    }, AssistsViewModel.GameProperties, new Game.ConditionProperties {
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

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

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

        public static bool Run(CarObject car = null, string carSkinId = null, TrackBaseObject track = null, string carSetupId = null) {
            return new ViewModel(string.Empty, false, car, carSkinId, track, carSetupId).Run();
        }

        public static async Task<bool> RunAsync(CarObject car = null, string carSkinId = null, TrackBaseObject track = null, string carSetupId = null,
                string weatherId = null, int? time = null) {
            var model = new ViewModel(string.Empty, false, car, carSkinId, track, carSetupId, weatherId, time);
            if (!model.GoCommand.CanExecute(null)) return false;
            await model.Go();
            return true;
        }

        public static bool RunPreset(string presetFilename, CarObject car = null, string carSkinId = null, TrackBaseObject track = null,
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

        private static void NavigateToPage() {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.NavigateTo(new Uri("/Pages/Drive/QuickDrive.xaml", UriKind.Relative));
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

        private void MoreConditionsOptions_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            // TODO: Don’t reopen if menu just have closed
            ConditionsOptions.IsOpen = true;
        }
    }
}
