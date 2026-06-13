using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Drive {
    public partial class CspNewModes {
        public CspNewModes() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Model.OnLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.OnUnloaded();
        }

        private void OnCarBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            Model.ChangeCarCommand.Execute(null);
        }

        private void OnTrackBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            Model.ChangeTrackCommand.Execute(null);
        }

        internal class ViewModel : NotifyPropertyChanged {
            private const string KeySaveable = "__CspNewModes_Main";
            private const string KeySelectedMode = ".CspNewModes.SelectedMode";

            public BetterObservableCollection<NewRaceModeData> Modes => NewRaceModeData.Instance.Items;

            private NewRaceModeData _selectedMode;

            [CanBeNull]
            public NewRaceModeData SelectedMode {
                get => _selectedMode;
                set {
                    if (Equals(value, _selectedMode)) return;
                    _selectedMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedMode));
                    OnSelectedModeChanged();
                    ValuesStorage.Set(KeySelectedMode, value?.Id);
                }
            }

            public bool HasSelectedMode => _selectedMode != null;

            private QuickDrive_Custom.ViewModel _modeViewModel;

            [CanBeNull]
            public QuickDrive_Custom.ViewModel ModeViewModel {
                get => _modeViewModel;
                private set {
                    if (Equals(value, _modeViewModel)) return;
                    _modeViewModel?.Unload();
                    _modeViewModel = value;
                    OnPropertyChanged();
                }
            }

            #region Car / Track
            private CarObject _selectedCar;

            [CanBeNull]
            public CarObject SelectedCar {
                get => _selectedCar;
                set {
                    if (Equals(value, _selectedCar)) return;
                    _selectedCar = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    SaveLater();
                    ModeViewModel?.OnSelectedUpdated(value, SelectedTrack);
                }
            }

            private TrackObjectBase _selectedTrack;

            [CanBeNull]
            public TrackObjectBase SelectedTrack {
                get => _selectedTrack;
                set {
                    if (Equals(value, _selectedTrack)) return;
                    _selectedTrack = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GoCommand));
                    SaveLater();
                    ModeViewModel?.OnSelectedUpdated(SelectedCar, value);
                    FancyBackgroundManager.Instance.ChangeBackground(value?.PreviewImage);
                }
            }
            #endregion

            #region Conditions
            private int _time = 12 * 60 * 60;

            public int Time {
                get => _time;
                set {
                    value = value.Clamp(0, 24 * 60 * 60 - 1);
                    if (value == _time) return;
                    _time = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    RefreshSelectedWeatherObject();
                    SaveLater();
                }
            }

            public string DisplayTime => $"{_time / 3600:D2}:{(_time % 3600) / 60:D2}";

            private double _temperature = 22.0;

            public double Temperature {
                get => _temperature;
                set {
                    value = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                    if (Equals(value, _temperature)) return;
                    _temperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    RefreshSelectedWeatherObject();
                    SaveLater();
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeatherObject?.TemperatureCoefficient ?? 0.0);

            private object _selectedWeather;

            public object SelectedWeather {
                get => _selectedWeather;
                set {
                    if (Equals(value, _selectedWeather)) return;
                    _selectedWeather = value;
                    OnPropertyChanged();
                    RefreshSelectedWeatherObject();
                    OnPropertyChanged(nameof(RoadTemperature));
                    SaveLater();
                }
            }

            [CanBeNull]
            public WeatherObject SelectedWeatherObject { get; private set; }

            private void RefreshSelectedWeatherObject() {
                var o = WeatherTypeWrapped.Unwrap(SelectedWeather, Time, Temperature);
                if (o != SelectedWeatherObject) {
                    SelectedWeatherObject = o;
                    OnPropertyChanged(nameof(SelectedWeatherObject));
                    OnPropertyChanged(nameof(RoadTemperature));
                }
            }

            private int _timeMultiplier = 1;

            public int TimeMultiplier {
                get => _timeMultiplier;
                set {
                    if (value == _timeMultiplier) return;
                    _timeMultiplier = value.Clamp(0, 3600);
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _windSpeedMin = 5;

            public double WindSpeedMin {
                get => _windSpeedMin;
                set {
                    if (Equals(value, _windSpeedMin)) return;
                    _windSpeedMin = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _windSpeedMax = 10;

            public double WindSpeedMax {
                get => _windSpeedMax;
                set {
                    if (Equals(value, _windSpeedMax)) return;
                    _windSpeedMax = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double _windDirection;

            public double WindDirection {
                get => _windDirection;
                set {
                    if (Equals(value, _windDirection)) return;
                    _windDirection = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }
            #endregion

            #region Assists & Track State
            public AssistsViewModel AssistsViewModel => AssistsViewModel.Instance;
            public TrackStateViewModel TrackState => TrackStateViewModel.Instance;
            #endregion

            #region Commands
            private ICommand _changeCarCommand;

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new DelegateCommand(() => {
                var dialog = new SelectCarDialog(SelectedCar).ApplyDefault(ModeViewModel?.GetDefaultCarFilter());
                dialog.ShowDialog();
                if (!dialog.IsResultOk || dialog.SelectedCar == null) return;
                var car = dialog.SelectedCar;
                car.SelectedSkin = dialog.SelectedSkin;
                SelectedCar = car;
            }));

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new DelegateCommand(() => {
                SelectedTrack = SelectTrackDialog.Show(SelectedTrack, ModeViewModel?.GetDefaultTrackFilter());
            }));

            private CommandBase _goCommand;

            public ICommand GoCommand => _goCommand ?? (_goCommand =
                    new AsyncCommand(Go, () => SelectedCar != null && SelectedTrack != null && ModeViewModel != null));

            private async Task Go() {
                var car = SelectedCar;
                var track = SelectedTrack;
                var modeVm = ModeViewModel;
                if (car == null || track == null || modeVm == null) return;

                var weather = SelectedWeatherObject ?? WeatherManager.Instance.GetDefault();

                try {
                    await modeVm.Drive(
                            new Game.BasicProperties {
                                CarId = car.Id,
                                CarSkinId = car.SelectedSkin?.Id,
                                TrackId = track.Id,
                                TrackConfigurationId = track.LayoutId
                            },
                            AssistsViewModel.ToGameProperties(),
                            new Game.ConditionProperties {
                                AmbientTemperature = Temperature,
                                RoadTemperature = Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                                        weather?.TemperatureCoefficient ?? 1d),
                                SunAngle = Game.ConditionProperties.GetSunAngle(Time),
                                TimeMultiplier = TimeMultiplier,
                                CloudSpeed = 0.2,
                                WeatherName = weather?.Id,
                                WindDirectionDeg = WindDirection,
                                WindSpeedMin = WindSpeedMin,
                                WindSpeedMax = WindSpeedMax,
                            },
                            TrackState.ToProperties(),
                            string.Empty,
                            new object[] {
                                new WeatherSpecificDate(false, DateTime.Now),
                                new WeatherDetails(null, SelectedWeather as WeatherTypeWrapped,
                                        WeatherFxControllerData.Instance.Items.FirstOrDefault(x => x.IsSelectedAsBase)?.Id),
                                TrackState.WeatherDefined
                                        ? new CustomTrackState(Path.Combine(weather?.Location ?? ".", "track_state.ini"))
                                        : null
                            });
                } finally {
                    _goCommand?.RaiseCanExecuteChanged();
                }
            }
            #endregion

            #region Save/Load
            private class SaveableData {
                [JsonProperty("cid")]
                public string CarId;

                [JsonProperty("tid")]
                public string TrackId;

                [JsonProperty("wid")]
                public string WeatherId;

                [JsonProperty("tmp")]
                public double Temperature = 22.0;

                [JsonProperty("tim")]
                public int Time = 12 * 60 * 60;

                [JsonProperty("tmx")]
                public int TimeMultiplier = 1;

                [JsonProperty("wsm")]
                public double WindSpeedMin = 5;

                [JsonProperty("wsx")]
                public double WindSpeedMax = 10;

                [JsonProperty("wdr")]
                public double WindDirection;
            }

            private readonly ISaveHelper _saveable;

            private void SaveLater() {
                _saveable?.SaveLater();
            }
            #endregion

            public ViewModel() {
                _saveable = new SaveHelper<SaveableData>(KeySaveable, () => new SaveableData {
                    CarId = SelectedCar?.Id,
                    TrackId = SelectedTrack?.IdWithLayout,
                    WeatherId = WeatherTypeWrapped.Serialize(SelectedWeather),
                    Temperature = Temperature,
                    Time = Time,
                    TimeMultiplier = TimeMultiplier,
                    WindSpeedMin = WindSpeedMin,
                    WindSpeedMax = WindSpeedMax,
                    WindDirection = WindDirection,
                }, o => {
                    Temperature = o.Temperature;
                    Time = o.Time;
                    TimeMultiplier = o.TimeMultiplier;
                    WindSpeedMin = o.WindSpeedMin;
                    WindSpeedMax = o.WindSpeedMax;
                    WindDirection = o.WindDirection;
                    if (o.CarId != null) SelectedCar = CarsManager.Instance.GetById(o.CarId) ?? SelectedCar;
                    if (o.TrackId != null) SelectedTrack = TracksManager.Instance.GetLayoutById(o.TrackId) ?? SelectedTrack;
                    SelectedWeather = WeatherTypeWrapped.Deserialize(o.WeatherId) ?? (object)WeatherManager.Instance.GetDefault();
                }, () => {
                    Temperature = 22.0;
                    Time = 12 * 60 * 60;
                    TimeMultiplier = 1;
                    WindSpeedMin = 5;
                    WindSpeedMax = 10;
                    WindDirection = 0;
                    SelectedCar = CarsManager.Instance.GetDefault();
                    SelectedTrack = TracksManager.Instance.GetDefault();
                    SelectedWeather = WeatherManager.Instance.GetDefault();
                });

                _saveable.LoadOrReset();

                // Restore selected mode
                var savedModeId = ValuesStorage.Get<string>(KeySelectedMode);
                if (savedModeId != null && NewRaceModeData.Instance.IsReady) {
                    SelectedMode = Modes.GetByIdOrDefault(savedModeId);
                } else if (NewRaceModeData.Instance.IsReady) {
                    SelectedMode = Modes.FirstOrDefault();
                }
            }

            private void OnSelectedModeChanged() {
                try {
                    ModeViewModel = _selectedMode != null ? new QuickDrive_Custom.ViewModel(_selectedMode.Id) : null;
                    ModeViewModel?.OnSelectedUpdated(SelectedCar, SelectedTrack);
                } catch (Exception e) {
                    Logging.Error($"Failed to create mode ViewModel: {e}");
                    ModeViewModel = null;
                }
            }

            public void OnLoaded() {
                NewRaceModeData.Instance.Reloaded += OnModesReloaded;
                if (NewRaceModeData.Instance.IsReady && _selectedMode == null && Modes.Count > 0) {
                    var savedModeId = ValuesStorage.Get<string>(KeySelectedMode);
                    SelectedMode = (savedModeId != null ? Modes.GetByIdOrDefault(savedModeId) : null) ?? Modes.FirstOrDefault();
                }
            }

            public void OnUnloaded() {
                NewRaceModeData.Instance.Reloaded -= OnModesReloaded;
                ModeViewModel?.Unload();
            }

            private void OnModesReloaded(object sender, EventArgs e) {
                OnPropertyChanged(nameof(Modes));
                if (_selectedMode != null && !Modes.Contains(_selectedMode)) {
                    SelectedMode = Modes.FirstOrDefault();
                }
            }
        }
    }
}
