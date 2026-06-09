using System;
using System.ComponentModel;
using System.Linq;
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
using FirstFloor.ModernUI;
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
                    if (!Apply(value, ref _realConditions)) return;
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
                    if (!Apply(value, ref _idealConditions)) return;
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
                set => Apply(value, ref _realConditionsManualTime, () => {
                    SaveLater();
                    UpdateConditions();
                });
            }

            private bool _realConditionsManualWind;

            public bool RealConditionsManualWind {
                get => _realConditionsManualWind;
                set => Apply(value, ref _realConditionsManualWind, () => {
                    SaveLater();
                    UpdateConditions();
                });
            }

            private bool _realConditionsLocalWeather;

            public bool RealConditionsLocalWeather {
                get => _realConditionsLocalWeather;
                set => Apply(value, ref _realConditionsLocalWeather, () => {
                    SaveLater();
                    UpdateConditions();
                });
            }

            private bool _realConditionsTimezones;

            public bool RealConditionsTimezones {
                get => _realConditionsTimezones;
                set => Apply(value, ref _realConditionsTimezones, () => {
                    SaveLater();
                    UpdateConditions();
                });
            }
            #endregion

            #region Mode-related variables
            private bool _manualTime;

            public bool ManualTime {
                get => _manualTime;
                private set => Apply(value, ref _manualTime, () => {
                    if (value) {
                        IsTimeClamped = false;
                    }
                });
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
                set => Apply(value, ref _selectedWeatherType, () => {
                    if (_selectedWeatherType != WeatherType.None && !RealConditions) {
                        TryToSetWeather();
                    }
                });
            }

            private object _selectedWeather;

            public WeatherTypeWrapped SelectedWeatherWrapped => SelectedWeather as WeatherTypeWrapped;

            /// <summary>
            /// Null for random weather, WeatherObject for specific weather, WeatherTypeWrapped for weather-by-type.
            /// </summary>
            [CanBeNull]
            public object SelectedWeather {
                get => _selectedWeather;
                set => Apply(value, ref _selectedWeather, () => {
                    RefreshSelectedWeatherObject();

                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));
                    OnPropertyChanged(nameof(SelectedWeatherWrapped));

                    if (!RealConditions) {
                        SaveLater();

                        if (PatchHelper.IsWeatherFxActive()) {
                            if (value is WeatherTypeWrapped weatherType 
                                && weatherType.TypeOpt >= WeatherType.LightThunderstorm && weatherType.TypeOpt <= WeatherType.HeavySleet) {
                                if (SelectedCar?.UseExtendedPhysics == false && PatchHelper.IsRainFxActive()) {
                                    Logging.Debug("Triggering extended physics hint");
                                    FancyHints.ExtendedPhysics.Trigger();
                                } else {
                                    FancyHints.ExtendedPhysics.MarkAsUnnecessary();
                                }
                            }
                            return;
                        }
                        
                        if (value is WeatherObject weather) {
                            var diapason = weather.GetTimeDiapason();
                            var timeFits = diapason?.Contains(Time);
                            if (timeFits == true) {
                                IsTimeOutOfWeatherRange = false;
                            } else if (IsTimeUnusual() || weather.IsWeatherTimeUnusual()) {
                                Time = diapason?.FindClosest(Time) ?? 12 * 60 * 60;
                            } else {
                                IsTimeOutOfWeatherRange = timeFits == false;
                            }
                        } else if (value is WeatherTypeWrapped type && !WeatherManager.Instance.Enabled.Any(x => x.Fits(type.TypeOpt, Time, null))) {
                            var diapason = Diapason.CreateTime(string.Empty);
                            var basicAdded = false;
                            foreach (var d in WeatherManager.Instance.Enabled.Where(x => x.Fits(type.TypeOpt, null, null)).Select(x => x.GetTimeDiapason())) {
                                if (d != null) {
                                    diapason.CombineWith(d);
                                } else if (!basicAdded) {
                                    diapason.CombineWith(GetBasicTimeDiapason());
                                    basicAdded = true;
                                }
                            }
                            Time = diapason.FindClosest(Time);
                        }
                    } 
                });
            }

            [CanBeNull]
            public WeatherObject SelectedWeatherObject { get; private set; }

            private void RefreshSelectedWeatherObject() {
                var o = WeatherTypeWrapped.Unwrap(SelectedWeather, Time, Temperature);
                if (o == null && IsTimeUnusual()) {
                    SelectedWeather = FindFittingWeather();
                } else if (o != SelectedWeatherObject) {
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
                    var weatherObject = GetRandomObject(WeatherManager.Instance, SelectedWeatherObject?.Id, null);
                    if (weatherObject == null) return null;
                    if (weatherObject.Fits(time, temperature) || i == 100) return weatherObject;
                }
            }

            private void SetRandomWeather(bool considerConditions) {
                RealConditions = false;
                SelectedWeather = GetRandomWeather(considerConditions ? Time : (int?)null, considerConditions ? Temperature : (double?)null);
            }

            private DelegateCommand _randomWeatherCommand;

            public DelegateCommand RandomWeatherCommand
                => _randomWeatherCommand ?? (_randomWeatherCommand = new DelegateCommand(() => SetRandomWeather(true)));
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

                    var address = await Prompt.ShowAsync("Where are you?", "Local address", localAddress, @"?", required: true);
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
                    if (!Apply(value, ref _temperature)) return;
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
                set => Apply(value, ref _randomTemperature, SaveLater);
            }

            private double GetRandomTemperature() {
                for (var i = 0;; i++) {
                    var value = MathUtils.Random(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                    if (SelectedWeatherObject?.GetTemperatureDiapason()?.Contains(value) != false || i == 100) {
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
                set => Apply(value, ref _customRoadTemperature, () => {
                    OnPropertyChanged(nameof(RoadTemperature));
                    SaveLater();
                });
            }

            private double? _customRoadTemperatureValue;

            public double CustomRoadTemperatureValue {
                get => _customRoadTemperatureValue ?? RecommendedRoadTemperature;
                set {
                    value = value.Round(0.1).Clamp(CommonAcConsts.RoadTemperatureMinimum,
                            SettingsHolder.Drive.QuickDriveExpandBounds ? CommonAcConsts.RoadTemperatureMaximum * 2 : CommonAcConsts.RoadTemperatureMaximum);
                    if (!Apply(value, ref _customRoadTemperatureValue)) return;
                    OnPropertyChanged(nameof(RoadTemperature));
                    SaveLater();
                }
            }

            private Diapason<int> GetBasicTimeDiapason() {
                var result = Diapason.CreateTime(string.Empty);
                result.Pieces.Add(PatchHelper.IsFeatureSupported(PatchHelper.FeatureFullDay)
                        ? new Diapason<int>.Piece(0, CommonAcConsts.TimeAbsoluteMaximum)
                        : new Diapason<int>.Piece(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum));
                return result;
            }

            private void UpdateTimeDiapason() {
                var allowed = GetBasicTimeDiapason();
                foreach (var weatherObject in WeatherManager.Instance.Enabled) {
                    var time = weatherObject.GetTimeDiapason();
                    if (time != null) {
                        allowed.CombineWith(time);
                    }
                }

                TimeSliderMapper = new DiapasonMapper(allowed) {
                    ActualValue = Time
                };
            }

            private DiapasonMapper _timeSliderMapper;

            [CanBeNull]
            public DiapasonMapper TimeSliderMapper {
                get => _timeSliderMapper;
                set {
                    var oldValue = _timeSliderMapper;
                    Apply(value, ref _timeSliderMapper, () => {
                        oldValue?.UnsubscribeWeak(OnTimeSliderMapperChanged);
                        value?.SubscribeWeak(OnTimeSliderMapperChanged);
                        Time = value?.GetClosest(Time) ?? Time;
                    });
                }
            }

            private readonly Busy _syncTime = new Busy();

            private void OnTimeSliderMapperChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (propertyChangedEventArgs.PropertyName == nameof(TimeSliderMapper.ActualValue)) {
                    _syncTime.Do(() => Time = TimeSliderMapper?.ActualValue ?? Time);
                }
            }

            private int _time;
            private WeatherObject _previousWeatherObject;

            public int Time {
                get => _time;
                set {
                    if (!Apply(TimeSliderMapper?.GetClosest(value) ?? value, ref _time)) return;
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));

                    _syncTime.Do(() => {
                        if (TimeSliderMapper != null) {
                            TimeSliderMapper.ActualValue = Time;
                        }
                    });

                    if (!RealConditions || RealConditionsManualTime) {
                        SaveLater();
                    }

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    } else if (SelectedWeather is WeatherObject weather) {
                        var fits = weather.GetTimeDiapason()?.Contains(_time);
                        if (!IsTimeUnusual()) {
                            if (fits == false && weather.IsWeatherTimeUnusual()) {
                                SelectedWeather = _previousWeatherObject ?? FindFittingWeather();
                            } else {
                                IsTimeOutOfWeatherRange = fits == false;
                            }
                        } else if (fits != true) {
                            Logging.Warning("Time doesn’t fit: " + _time + ", diapason: " + weather.TimeDiapason);
                            if (!weather.IsWeatherTimeUnusual()) {
                                _previousWeatherObject = weather;
                            }

                            SelectedWeather = FindFittingWeather();
                        }
                    } else {
                        RefreshSelectedWeatherObjectLater();
                    }
                }
            }

            private bool IsTimeUnusual(int time) {
                return PatchHelper.ClampTime(time) != time;
            }

            private bool IsTimeUnusual() {
                return IsTimeUnusual(Time);
            }

            [CanBeNull]
            private WeatherObject FindFittingWeather() {
                var isUnusual = IsTimeUnusual();
                var allowed = WeatherManager.Instance.Enabled.Where(x => x.GetTimeDiapason()?.Contains(_time) ?? !isUnusual).ToList();
                return allowed.Where(x => x.GetTemperatureDiapason()?.Contains(Temperature) == true).RandomElementOrDefault()
                        ?? allowed.RandomElementOrDefault();
            }

            private bool _useSpecificDate;

            public bool UseSpecificDate {
                get => _useSpecificDate;
                set => Apply(value, ref _useSpecificDate, SaveLater);
            }

            private DateTime _specificDateValue;

            public DateTime SpecificDateValue {
                get => _specificDateValue;
                set => Apply(value.ToUnixTimestamp() < TimeSpan.FromHours(12).TotalSeconds ? DateTime.Now : value,
                        ref _specificDateValue, SaveLater);
            }

            private bool _randomTime;

            public bool RandomTime {
                get => _randomTime;
                set => Apply(value, ref _randomTime, SaveLater);
            }

            private int GetRandomTime() {
                var weather = SelectedWeatherObject;
                var mapper = TimeSliderMapper;
                for (var i = 0;; i++) {
                    if (mapper != null) {
                        var actualValue = mapper.MappedToActual(MathUtils.Random(mapper.Size));
                        if (weather == null
                                || i == 100
                                || (weather.GetTimeDiapason()?.Contains(actualValue) ?? !IsTimeUnusual(actualValue))) {
                            return actualValue;
                        }
                    } else {
                        var value = MathUtils.Random(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
                        if (SelectedWeatherObject?.GetTimeDiapason()?.Contains(value) != false || i == 100) {
                            return value;
                        }
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
                get => _time.ToDisplayTime();
                set {
                    if (!FlexibleParser.TryParseTime(value, out var time)) return;
                    Time = time;
                }
            }

            #region Wind
            private int _windDirection;

            public int WindDirection {
                get => _windDirection;
                set => Apply(((value % 360 + 360) % 360).Round(), ref _windDirection, () => {
                    OnPropertyChanged(nameof(DisplayWindDirection));
                    OnPropertyChanged(nameof(WindDirectionFlipped));
                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                });
            }

            public int WindDirectionFlipped {
                get => (_windDirection + 180) % 360;
                set => WindDirection = (value - 180) % 360;
            }

            private double _windSpeedMin;

            public double WindSpeedMin {
                get => _windSpeedMin;
                set => Apply(value.Round(0.1), ref _windSpeedMin, () => {
                    if (WindSpeedMax < value) {
                        WindSpeedMax = value;
                    }

                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                });
            }

            private double _windSpeedMax;

            public double WindSpeedMax {
                get => _windSpeedMax;
                set => Apply(value.Round(0.1), ref _windSpeedMax, () => {
                    if (WindSpeedMin > value) {
                        WindSpeedMin = value;
                    }

                    if (!RealConditions || RealConditionsManualWind) {
                        SaveLater();
                    }
                });
            }

            public string DisplayWindDirection => _windDirection.ToDisplayWindDirection(RandomWindDirection);

            private bool _randomWindSpeed;

            public bool RandomWindSpeed {
                get => _randomWindSpeed;
                set => Apply(value, ref _randomWindSpeed, SaveLater);
            }

            private bool _randomWindDirection;

            public bool RandomWindDirection {
                get => _randomWindDirection;
                set => Apply(value, ref _randomWindDirection, () => {
                    OnPropertyChanged(nameof(DisplayWindDirection));
                    SaveLater();
                });
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
                    SpecificDateValue = DateTime.Now;
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
                            await RealConditionsHelper.UpdateConditionsAsync(SelectedTrack, RealConditionsLocalWeather, RealConditionsTimezones,
                                    RealConditionsManualTime ? default(Action<DateTime>) : TryToSetTime, weather => {
                                        RealWeather = weather;
                                        TryToSetTemperature(weather.Temperature);
                                        SelectedWeatherType = weather.Type;
                                        TryToSetWeather();

                                        if (!RealConditionsManualWind) {
                                            WindDirection = ((weather.WindDirection + 180) % 360).RoundToInt();
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

            private void TryToSetTime(DateTime value) {
                var seconds = value.TimeOfDay.TotalSeconds.RoundToInt();
                var clamped = TimeSliderMapper?.GetClosest(seconds) ?? seconds;
                IsTimeClamped = clamped != seconds;
                Time = clamped;
                SpecificDateValue = value;
            }

            private void TryToSetTemperature(double value) {
                var clamped = value.Clamp(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                IsTemperatureClamped = value < CommonAcConsts.TemperatureMinimum || value > CommonAcConsts.TemperatureMaximum;
                Temperature = clamped;
            }

            private readonly WeatherTypeConverterState _weatherTypeHelper = new WeatherTypeConverterState();

            private void TryToSetWeather() {
                if (PatchHelper.IsWeatherFxActive()) {
                    SelectedWeather = new WeatherTypeWrapped(SelectedWeatherType);
                } else {
                    var weather = _weatherTypeHelper.TryToGetWeather(SelectedWeatherType, Time, Temperature);
                    if (weather != null) {
                        SelectedWeather = weather;
                    }
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
            FancyHints.MoreDriveAssists.MarkAsUnnecessary();
        }
    }
}