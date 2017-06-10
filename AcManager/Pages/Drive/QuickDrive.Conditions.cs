using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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

                    if (value is WeatherObject weatherObject) {
                        SelectedWeatherObject = weatherObject;
                    } else {
                        SelectedWeatherObject = WeatherComboBox.Unwrap(value);
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));
                    OnPropertyChanged(nameof(RecommendedRoadTemperature));
                    OnPropertyChanged(nameof(SelectedWeatherObject));

                    if (!RealConditions) {
                        SaveLater();
                    }
                }
            }

            [CanBeNull]
            public WeatherObject SelectedWeatherObject { get; private set; }

            [CanBeNull]
            private WeatherObject GetRandomWeather([CanBeNull] double? temperatureToConsider) {
                var weatherObject = SelectedWeatherObject;
                for (var i = 0; i < 100; i++) {
                    weatherObject = GetRandomObject(WeatherManager.Instance, SelectedWeatherObject?.Id);
                    if (!temperatureToConsider.HasValue || weatherObject.TemperatureDiapason?.DiapasonContains(temperatureToConsider.Value) != false) {
                        break;
                    }
                }
                return weatherObject;
            }

            private void SetRandomWeather(bool considerTemperature) {
                RealConditions = false;
                SelectedWeather = GetRandomWeather(considerTemperature ? Temperature : (double?)null);
            }

            private DelegateCommand _randomWeatherCommand;

            public DelegateCommand RandomWeatherCommand => _randomWeatherCommand ?? (_randomWeatherCommand = new DelegateCommand(() => {
                SetRandomWeather(true);
            }));
            #endregion

            #region Automatically set variables
            private WeatherDescription _realWeather;

            [CanBeNull]
            public WeatherDescription RealWeather {
                get => _realWeather;
                set {
                    if (Equals(value, _realWeather)) return;
                    _realWeather = value;
                    OnPropertyChanged();
                }
            }

            private bool _isTimeClamped;
            public bool IsTimeClamped {
                get => _isTimeClamped;
                set {
                    if (value == _isTimeClamped) return;
                    _isTimeClamped = value;
                    OnPropertyChanged();
                }
            }

            private bool _isTemperatureClamped;
            public bool IsTemperatureClamped {
                get => _isTemperatureClamped;
                set {
                    if (value == _isTemperatureClamped) return;
                    _isTemperatureClamped = value;
                    OnPropertyChanged();
                }
            }

            private bool _isWeatherNotSupported;
            public bool IsWeatherNotSupported {
                get => _isWeatherNotSupported;
                set {
                    if (value == _isWeatherNotSupported) return;
                    _isWeatherNotSupported = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            private ICommand _switchLocalWeatherCommand;

            public ICommand SwitchLocalWeatherCommand => _switchLocalWeatherCommand ?? (_switchLocalWeatherCommand = new AsyncCommand(async () => {
                if (string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                    var entry = await Task.Run(() => IpGeoProvider.Get());
                    var localAddress = entry == null ? "" : $"{entry.City}, {entry.Country}";

                    var address = Prompt.Show("Where are you?", "Local Address", localAddress, @"?", required: true);
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
                var diapason = SelectedWeatherObject?.TemperatureDiapason;
                var temperature = Temperature;
                for (var i = 0; i < 100; i++) {
                    temperature = MathUtils.Random(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
                    if (diapason?.DiapasonContains(temperature) != false) break;
                }
                return temperature;
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
                    value = value.Round(0.1).Clamp(CommonAcConsts.TemperatureMinimum,
                            SettingsHolder.Drive.QuickDriveExpandBounds ? CommonAcConsts.TemperatureMaximum * 2 : CommonAcConsts.TemperatureMaximum);
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

            private DelegateCommand _randomTimeCommand;

            public DelegateCommand RandomTimeCommand => _randomTimeCommand ?? (_randomTimeCommand = new DelegateCommand(() => {
                if (RealConditions) {
                    RealConditionsManualTime = true;
                }

                RandomTime = false;
                Time = MathUtils.Random(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
            }));

            public string DisplayTime {
                get => $"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}";
                set {
                    int time;
                    if (!FlexibleParser.TryParseTime(value, out time)) return;
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
                    if (!RealConditions) {
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
                    if (!RealConditions) {
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
                    if (!RealConditions) {
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
                    RealWeather = null;
                    _weatherTypeHelper.Reset();

                    Temperature = 26d;
                    Time = 12 * 60 * 60;
                    SelectedWeather = WeatherManager.Instance.GetDefault();
                    Controls.UserPresetsControl.LoadBuiltInPreset(TrackState.PresetableKey, TrackStateViewModelBase.PresetableCategory, "Optimum");
                    WindSpeedMin = 0d;
                    WindSpeedMax = 0d;
                } else if (!RealConditions) {
                    IsTemperatureClamped = false;
                    IsWeatherNotSupported = false;
                    ManualTime = true;
                    RealWeather = null;
                    _weatherTypeHelper.Reset();
                } else {
                    RandomTime = false;
                    RandomTemperature = false;
                    ManualTime = RealConditionsManualTime;

                    using (var cancellation = new CancellationTokenSource()) {
                        _updateCancellationTokenSource = cancellation;

                        try {
                            await RealConditionsHelper.UpdateConditions(SelectedTrack, RealConditionsLocalWeather, RealConditionsTimezones,
                                    RealConditionsManualTime ? default(Action<int>) : TryToSetTime, weather => {
                                        RealWeather = weather;
                                        TryToSetTemperature(weather.Temperature);
                                        SelectedWeatherType = weather.Type;
                                        TryToSetWeather();
                                        WindDirection = weather.WindDirection.RoundToInt();
                                        WindSpeedMin = weather.WindSpeed * 3.6;
                                        WindSpeedMax = weather.WindSpeed * 3.6;
                                    }, cancellation.Token);
                        } catch (TaskCanceledException) {} catch (Exception e) {
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

            private readonly WeatherTypeHelper _weatherTypeHelper = new WeatherTypeHelper();

            private void TryToSetWeather() {
                _weatherTypeHelper.SetParams(Time, Temperature);

                var weather = SelectedWeatherObject;
                if (_weatherTypeHelper.TryToGetWeather(SelectedWeatherType, ref weather)) {
                    SelectedWeather = weather;
                }
            }

            private bool _tryToSetWeatherLater;

            private async void TryToSetWeatherLater() {
                if (_tryToSetWeatherLater || _updateCancellationTokenSource != null || !RealConditions) return;
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
        }

        private void OnAssistsContextMenuButtonClick(object sender, ContextMenuButtonEventArgs e) {
            FancyHints.MoreDriveAssists.MaskAsUnnecessary();
        }
    }

    public class WeatherTypeHelper {
        private int _time;
        private double _temperature;

        public void SetParams(int time, double temperature) {
            _time = time;
            _temperature = temperature;
        }

        private string _weatherCandidatesFootprint;

        public bool TryToGetWeather(WeatherType type, [CanBeNull] ref WeatherObject weather) {
            if (type == WeatherType.None) return true;

            try {
                var candidates = WeatherManager.Instance.LoadedOnly.Where(x => x.Enabled && x.TemperatureDiapason?.DiapasonContains(_temperature) != false
                        && x.TimeDiapason?.TimeDiapasonContains(_time) != false).ToList();
                var closest = WeatherDescription.FindClosestWeather(from w in candidates select w.Type, type);
                if (closest == null) return false;

                candidates = candidates.Where(x => x.Type == closest).ToList();
                var footprint = candidates.Select(x => x.Id).JoinToString(';');
                if (footprint != _weatherCandidatesFootprint || !candidates.Contains(weather)) {
                    weather = candidates.RandomElementOrDefault();
                    _weatherCandidatesFootprint = footprint;
                }

                return true;
            } catch (Exception e) {
                Logging.Error(e);
                return false;
            }
        }

        public void Reset() {
            _weatherCandidatesFootprint = null;
        }

        [CanBeNull]
        public static WeatherObject TryToGetWeather(WeatherType type, int time, double temperature) {
            var helper = new WeatherTypeHelper();
            helper.SetParams(time, temperature);
            WeatherObject result = null;
            return helper.TryToGetWeather(type, ref result) ? result : null;
        }

        [CanBeNull]
        public static WeatherObject TryToGetWeather(WeatherDescription description, int time) {
            return TryToGetWeather(description.Type, time, description.Temperature);
        }
    }

    public static class RealConditionsHelper {
        private const int SecondsPerDay = 24 * 60 * 60;

        /// <summary>
        /// Complex method, but it’s the best I can think of for now. Due to async nature,
        /// all results will be returned in callbacks. There is no guarantee in which order callbacks
        /// will be called (and even if they will be called at all or not)!
        /// </summary>
        /// <param name="track">Track for which conditions will be loaded.</param>
        /// <param name="localWeather">Use local weather instead.</param>
        /// <param name="considerTimezones">Consider timezones while setting time. Be careful: you’ll get an unclamped time!</param>
        /// <param name="timeCallback">Set to null if you don’t need an automatic time.</param>
        /// <param name="weatherCallback">Set to null if you don’t need weather.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task UpdateConditions(TrackObjectBase track, bool localWeather, bool considerTimezones,
                [CanBeNull] Action<int> timeCallback, [CanBeNull] Action<WeatherDescription> weatherCallback, CancellationToken cancellation) {
            GeoTagsEntry trackGeoTags = null, localGeoTags = null;

            if (!localWeather || considerTimezones && timeCallback != null) {
                trackGeoTags = track.GeoTags;
                if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                    trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                    if (cancellation.IsCancellationRequested) return;
                }
            }

            if ((trackGeoTags == null || localWeather) && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                localGeoTags = await TracksLocator.TryToLocateAsync(SettingsHolder.Drive.LocalAddress);
                if (cancellation.IsCancellationRequested) return;
            }

            // Time
            var time = DateTime.Now.TimeOfDay.TotalSeconds.RoundToInt();
            if (timeCallback != null) {
                if (trackGeoTags == null || !considerTimezones) {
                    timeCallback.Invoke(time);
                } else {
                    var timeZone = await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
                    if (cancellation.IsCancellationRequested) return;

                    timeCallback.Invoke((time +
                            (int)(timeZone == null ? 0 : timeZone.BaseUtcOffset.TotalSeconds - TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds) +
                            SecondsPerDay) % SecondsPerDay);
                }
            }

            // Weather
            var tags = localWeather ? localGeoTags : trackGeoTags ?? localGeoTags;
            if (tags == null) return;

            var weather = await WeatherProvider.TryToGetWeatherAsync(tags);
            if (cancellation.IsCancellationRequested) return;

            if (weather != null) {
                weatherCallback?.Invoke(weather);
            }
        }
    }
}