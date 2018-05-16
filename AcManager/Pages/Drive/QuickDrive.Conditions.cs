using System;
using System.Threading;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive {
        /// <summary>
        /// Moved outside in a pity attempt to sort everything out.
        /// </summary>
        public partial class ViewModel {
            #region User-set variables which define working mode
            private bool _realConditions;

            public bool RealConditions {
                get => _realConditions;
                set {
                    if (Equals(value, _realConditions)) return;
                    _realConditions = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ManualConditions));

                    if (value) {
                        _idealConditions = false;
                        OnPropertyChanged(nameof(IdealConditions));
                    }

                    SaveLater();
                    UpdateConditions();
                }
            }

            private bool _idealConditions;

            public bool IdealConditions {
                get => _idealConditions;
                set {
                    if (Equals(value, _idealConditions)) return;
                    _idealConditions = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ManualConditions));

                    if (value) {
                        _realConditions = false;
                        OnPropertyChanged(nameof(RealConditions));
                    }

                    SaveLater();
                    UpdateConditions();
                }
            }

            public bool ManualConditions => !_realConditions && !_idealConditions;

            private bool _realConditionsManualTime;

            public bool RealConditionsManualTime {
                get => _realConditionsManualTime;
                set {
                    if (value == _realConditionsManualTime) return;
                    _realConditionsManualTime = value;
                    OnPropertyChanged();
                    SaveLater();
                    UpdateConditions();
                }
            }

            private bool _realConditionsManualWind;

            public bool RealConditionsManualWind {
                get => _realConditionsManualWind;
                set {
                    if (value == _realConditionsManualWind) return;
                    _realConditionsManualWind = value;
                    OnPropertyChanged();
                    SaveLater();
                    UpdateConditions();
                }
            }

            private bool _realConditionsLocalWeather;

            public bool RealConditionsLocalWeather {
                get => _realConditionsLocalWeather;
                set {
                    if (value == _realConditionsLocalWeather) return;
                    _realConditionsLocalWeather = value;
                    OnPropertyChanged();
                    SaveLater();
                    UpdateConditions();
                }
            }

            private bool _realConditionsTimezones;

            public bool RealConditionsTimezones {
                get => _realConditionsTimezones;
                set {
                    if (value == _realConditionsTimezones) return;
                    _realConditionsTimezones = value;
                    OnPropertyChanged();
                    SaveLater();
                    UpdateConditions();
                }
            }
            #endregion

            #region Mode-related variables
            private bool _manualTime;

            public bool ManualTime {
                get => _manualTime;
                private set {
                    if (Equals(value, _manualTime)) return;
                    _manualTime = value;
                    OnPropertyChanged();

                    if (value) {
                        IsTimeClamped = false;
                    }
                }
            }

            private bool _manualWind;

            public bool ManualWind {
                get => _manualWind;
                private set => Apply(value, ref _manualWind);
            }
            #endregion

            #region User-set variables which could also be set automatically
            private WeatherType _selectedWeatherType;

            public WeatherType SelectedWeatherType {
                get => _selectedWeatherType;
                set {
                    if (Equals(value, _selectedWeatherType)) return;
                    _selectedWeatherType = value;
                    OnPropertyChanged();

                    if (_selectedWeatherType != WeatherType.None && !RealConditions) {
                        TryToSetWeather();
                    }
                }
            }

            private object _selectedWeather;

            /// <summary>
            /// Null for random weather, WeatherObject for specific weather, WeatherTypeWrapped for weather-by-type.
            /// </summary>
            [CanBeNull]
            public object SelectedWeather {
                get => _selectedWeather;
                set {
                    if (Equals(value, _selectedWeather)) return;
                    _selectedWeather = value;

                    RefreshSelectedWeatherObject();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));

                    if (!RealConditions) {
                        SaveLater();
                        if (value is WeatherObject weather) {
                            IsTimeOutOfWeatherRange = weather.TimeDiapason?.TimeDiapasonContains(Time) == false;
                        }
                    }
                }
            }

            [CanBeNull]
            public WeatherObject SelectedWeatherObject { get; private set; }

            private void RefreshSelectedWeatherObject() {
                var o = WeatherComboBox.Unwrap(SelectedWeather, Time, Temperature);
                if (o != SelectedWeatherObject) {
                    SelectedWeatherObject = o;
                    OnPropertyChanged(nameof(SelectedWeatherObject));
                }
            }

            private readonly Busy _refreshWeatherObjectBusy = new Busy();

            private void RefreshSelectedWeatherObjectLater() {
                if (RealConditions) return;
                _refreshWeatherObjectBusy.DoDelay(() => {
                    if (RealConditions) return;
                    RefreshSelectedWeatherObject();
                }, 500);
            }

            [CanBeNull]
            private WeatherObject GetRandomWeather(int? time, double? temperature) {
                for (var i = 0;; i++) {
                    var weatherObject = GetRandomObject(WeatherManager.Instance, SelectedWeatherObject?.Id);
                    if (weatherObject.Fits(time, temperature) || i == 100) return weatherObject;
                }
            }

            private void SetRandomWeather(bool considerConditions) {
                RealConditions = false;
                SelectedWeather = GetRandomWeather(considerConditions ? Time : (int?)null, considerConditions ? Temperature : (double?)null);
            }

            private DelegateCommand _randomWeatherCommand;

            public DelegateCommand RandomWeatherCommand
                => _randomWeatherCommand ?? (_randomWeatherCommand = new DelegateCommand(() => { SetRandomWeather(true); }));
            #endregion

            #region Automatically set variables
            private WeatherDescription _realWeather;

            [CanBeNull]
            public WeatherDescription RealWeather {
                get => _realWeather;
                set => Apply(value, ref _realWeather);
            }

            private bool _isTimeClamped;

            public bool IsTimeClamped {
                get => _isTimeClamped;
                set => Apply(value, ref _isTimeClamped);
            }

            private bool _isTimeOutOfWeatherRange;

            public bool IsTimeOutOfWeatherRange {
                get => _isTimeOutOfWeatherRange;
                set => Apply(value, ref _isTimeOutOfWeatherRange);
            }

            private bool _isTemperatureClamped;

            public bool IsTemperatureClamped {
                get => _isTemperatureClamped;
                set => Apply(value, ref _isTemperatureClamped);
            }

            private bool _isWeatherNotSupported;

            public bool IsWeatherNotSupported {
                get => _isWeatherNotSupported;
                set => Apply(value, ref _isWeatherNotSupported);
            }
            #endregion

            private ICommand _switchLocalWeatherCommand;

            public ICommand SwitchLocalWeatherCommand => _switchLocalWeatherCommand ?? (_switchLocalWeatherCommand = new AsyncCommand(async () => {
                if (string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                    var entry = await IpGeoProvider.GetAsync();
                    var localAddress = entry == null ? "" : $"{entry.City}, {entry.Country}";

                    var address = Prompt.Show("Where are you?", "Local address", localAddress, @"?", required: true);
                    if (string.IsNullOrWhiteSpace(address)) {
                        if (address != null) {
                            ModernDialog.ShowMessage("Value is required");
                        }

                        return;
                    }

                    SettingsHolder.Drive.LocalAddress = address;
                }

                RealConditionsLocalWeather = !RealConditionsLocalWeather;
            }));

            private double _temperature;

            // default limit: 10/36
            public double Temperature {
                get => _temperature;
                set {
                    value = value.Round(0.1).Clamp(CommonAcConsts.TemperatureMinimum,
                            SettingsHolder.Drive.QuickDriveExpandBounds ? CommonAcConsts.TemperatureMaximum * 2 : CommonAcConsts.TemperatureMaximum);
                    if (Equals(value, _temperature)) return;
                    _temperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));

                    if (!_customRoadTemperatureValue.HasValue) {
                        OnPropertyChanged(nameof(CustomRoadTemperatureValue));
                    }

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    } else {
                        RefreshSelectedWeatherObjectLater();
                        SaveLater();
                    }
                }
            }

            private bool _randomTemperature;

            public bool RandomTemperature {
                get => _randomTemperature;
                set {
                    if (Equals(value, _randomTemperature)) return;
                    _randomTemperature = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private double GetRandomTemperature() {
                for (var i = 0;; i++) {
                    var value = MathUtils.Random(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                    if (SelectedWeatherObject?.TemperatureDiapason?.DiapasonContains(value) != false || i == 100) {
                        return value;
                    }
                }
            }

            private DelegateCommand _randomTemperatureCommand;

            public DelegateCommand RandomTemperatureCommand => _randomTemperatureCommand ?? (_randomTemperatureCommand = new DelegateCommand(() => {
                RealConditions = false;
                RandomTemperature = false;
                Temperature = GetRandomTemperature();
            }));

            public double RecommendedRoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeatherObject?.TemperatureCoefficient ?? 0.0);

            public double RoadTemperature => CustomRoadTemperature ? CustomRoadTemperatureValue : RecommendedRoadTemperature;

            private bool _customRoadTemperature;

            public bool CustomRoadTemperature {
                get => _customRoadTemperature;
                set {
                    if (Equals(value, _customRoadTemperature)) return;
                    _customRoadTemperature = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    SaveLater();
                }
            }

            private double? _customRoadTemperatureValue;

            public double CustomRoadTemperatureValue {
                get => _customRoadTemperatureValue ?? RecommendedRoadTemperature;
                set {
                    value = value.Round(0.1).Clamp(CommonAcConsts.RoadTemperatureMinimum,
                            SettingsHolder.Drive.QuickDriveExpandBounds ? CommonAcConsts.RoadTemperatureMaximum * 2 : CommonAcConsts.RoadTemperatureMaximum);
                    if (Equals(value, _customRoadTemperatureValue)) return;
                    _customRoadTemperatureValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    SaveLater();
                }
            }

            private int _time;

            public int Time {
                get => _time;
                set {
                    if (value == _time) return;
                    _time = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));

                    if (!RealConditions || RealConditionsManualTime) {
                        SaveLater();
                    }

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    } else if (SelectedWeather is WeatherObject weather) {
                        IsTimeOutOfWeatherRange = weather.TimeDiapason?.TimeDiapasonContains(value) == false;
                    } else {
                        RefreshSelectedWeatherObjectLater();
                    }
                }
            }

            private bool _randomTime;

            public bool RandomTime {
                get => _randomTime;
                set {
                    if (Equals(value, _randomTime)) return;
                    _randomTime = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private int GetRandomTime() {
                for (var i = 0;; i++) {
                    var value = MathUtils.Random(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                    if (SelectedWeatherObject?.TimeDiapason?.TimeDiapasonContains(value) != false || i == 100) {
                        return value;
                    }
                }
            }

            private DelegateCommand _randomTimeCommand;

            public DelegateCommand RandomTimeCommand => _randomTimeCommand ?? (_randomTimeCommand = new DelegateCommand(() => {
                if (RealConditions) {
                    RealConditionsManualTime = true;
                }

                RandomTime = false;
                Time = GetRandomTime();
            }));

            public string DisplayTime {
                get => $@"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}";
                set {
                    if (!FlexibleParser.TryParseTime(value, out var time)) return;
                    Time = time;
                }
            }

            #region Wind
            private int _windDirection;

            public int WindDirection {
                get => _windDirection;
                set {
                    value = ((value % 360 + 360) % 360).Round();
                    if (Equals(value, _windDirection)) return;
                    _windDirection = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWindDirection));
                    OnPropertyChanged(nameof(WindDirectionFlipped));
                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                }
            }

            public int WindDirectionFlipped {
                get => (_windDirection + 180) % 360;
                set => WindDirection = (value - 180) % 360;
            }

            private double _windSpeedMin;

            public double WindSpeedMin {
                get => _windSpeedMin;
                set {
                    value = value.Round(0.1);
                    if (Equals(value, _windSpeedMin)) return;
                    _windSpeedMin = value;

                    if (WindSpeedMax < value) {
                        WindSpeedMax = value;
                    }

                    OnPropertyChanged();
                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                }
            }

            private double _windSpeedMax;

            public double WindSpeedMax {
                get => _windSpeedMax;
                set {
                    value = value.Round(0.1);
                    if (Equals(value, _windSpeedMax)) return;
                    _windSpeedMax = value;

                    if (WindSpeedMin > value) {
                        WindSpeedMin = value;
                    }

                    OnPropertyChanged();
                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                }
            }

            public string DisplayWindDirection => _windDirection.ToDisplayWindDirection(RandomWindDirection);

            private bool _randomWindSpeed;

            public bool RandomWindSpeed {
                get => _randomWindSpeed;
                set {
                    if (Equals(value, _randomWindSpeed)) return;
                    _randomWindSpeed = value;
                    OnPropertyChanged();
                    SaveLater();
                }
            }

            private bool _randomWindDirection;

            public bool RandomWindDirection {
                get => _randomWindDirection;
                set {
                    if (Equals(value, _randomWindDirection)) return;
                    _randomWindDirection = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWindDirection));
                    SaveLater();
                }
            }
            #endregion

            private CancellationTokenSource _updateCancellationTokenSource;

            public async void UpdateConditions() {
                if (_saveable.IsLoading) return;
                _updateCancellationTokenSource?.Cancel();

                if (IdealConditions) {
                    IsTemperatureClamped = false;
                    IsWeatherNotSupported = false;
                    ManualTime = false;
                    ManualWind = false;
                    RealWeather = null;
                    _weatherTypeHelper.Reset();

                    Temperature = 26d;
                    Time = 12 * 60 * 60;
                    SelectedWeather = WeatherManager.Instance.GetDefault();
                    UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, "Optimum");
                    WindSpeedMin = 0d;
                    WindSpeedMax = 0d;
                } else if (!RealConditions) {
                    IsTemperatureClamped = false;
                    IsWeatherNotSupported = false;
                    ManualTime = true;
                    ManualWind = true;
                    RealWeather = null;
                    _weatherTypeHelper.Reset();
                } else {
                    RandomTime = false;
                    RandomTemperature = false;
                    IsTimeOutOfWeatherRange = false;
                    ManualTime = RealConditionsManualTime;
                    ManualWind = RealConditionsManualWind;

                    using (var cancellation = new CancellationTokenSource()) {
                        _updateCancellationTokenSource = cancellation;

                        try {
                            await RealConditionsHelper.UpdateConditions(SelectedTrack, RealConditionsLocalWeather, RealConditionsTimezones,
                                    RealConditionsManualTime ? default(Action<int>) : TryToSetTime, weather => {
                                        RealWeather = weather;
                                        TryToSetTemperature(weather.Temperature);
                                        SelectedWeatherType = weather.Type;
                                        TryToSetWeather();

                                        if (!RealConditionsManualWind) {
                                            WindDirection = weather.WindDirection.RoundToInt();
                                            WindSpeedMin = weather.WindSpeed * 3.6;
                                            WindSpeedMax = weather.WindSpeed * 3.6;
                                        }
                                    }, cancellation.Token);
                        } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                            Logging.Warning(e);
                        }

                        _updateCancellationTokenSource = null;
                    }
                }
            }

            private void TrackUpdated() {
                UpdateConditions();
            }

            private void TryToSetTime(int value) {
                var clamped = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                IsTimeClamped = clamped != value;
                Time = clamped;
            }

            private void TryToSetTemperature(double value) {
                var clamped = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                IsTemperatureClamped = value < CommonAcConsts.TemperatureMinimum || value > CommonAcConsts.TemperatureMaximum;
                Temperature = clamped;
            }

            private readonly WeatherTypeConverterState _weatherTypeHelper = new WeatherTypeConverterState();

            private void TryToSetWeather() {
                var weather = _weatherTypeHelper.TryToGetWeather(SelectedWeatherType, Time, Temperature);
                if (weather != null) {
                    SelectedWeather = weather;
                }
            }

            private readonly Busy _tryToSetWeatherBusy = new Busy();

            private void TryToSetWeatherLater() {
                if (_updateCancellationTokenSource != null || !RealConditions) return;
                _tryToSetWeatherBusy.DoDelay(() => {
                    if (_updateCancellationTokenSource != null || !RealConditions) return;
                    TryToSetWeather();
                }, 500);
            }
        }

        private void OnAssistsContextMenuButtonClick(object sender, ContextMenuButtonEventArgs e) {
            FancyHints.MoreDriveAssists.MaskAsUnnecessary();
        }
    }
}