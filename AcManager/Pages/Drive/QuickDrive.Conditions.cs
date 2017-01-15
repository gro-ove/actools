using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Converters;
using AcManager.Controls.Dialogs;
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
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public partial class QuickDrive {
        /// <summary>
        /// Moved outside in a pity attempt to sort everything out.
        /// </summary>
        public partial class ViewModel {
            #region Constants and other non-changeable values
            public AcEnabledOnlyCollection<WeatherObject> WeatherList { get; } = WeatherManager.Instance.EnabledOnlyCollection;
            #endregion

            #region User-set variables which define working mode
            private bool _realConditions;
            public bool RealConditions {
                get { return _realConditions; }
                set {
                    if (Equals(value, _realConditions)) return;
                    _realConditions = value;
                    OnPropertyChanged();
                    SaveLater();
                    UpdateConditions();
                }
            }

            private bool _realConditionsManualTime;
            public bool RealConditionsManualTime {
                get { return _realConditionsManualTime; }
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
                get { return _realConditionsLocalWeather; }
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
                get { return _realConditionsTimezones; }
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
                get { return _manualTime; }
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
                get { return _selectedWeatherType; }
                set {
                    if (Equals(value, _selectedWeatherType)) return;
                    _selectedWeatherType = value;
                    OnPropertyChanged();

                    if (_selectedWeatherType != WeatherType.None && !RealConditions) {
                        TryToSetWeather();
                    }
                }
            }

            private WeatherObject _selectedWeather;

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

            private DelegateCommand _randomWeatherCommand;

            public DelegateCommand RandomWeatherCommand => _randomWeatherCommand ?? (_randomWeatherCommand = new DelegateCommand(() => {
                RealConditions = false;
                SelectedWeather = GetRandomObject(WeatherManager.Instance, SelectedWeather?.Id);
            }));
            #endregion

            #region Automatically set variables
            private WeatherDescription _realWeather;

            [CanBeNull]
            public WeatherDescription RealWeather {
                get { return _realWeather; }
                set {
                    if (Equals(value, _realWeather)) return;
                    _realWeather = value;
                    OnPropertyChanged();
                }
            }

            private bool _isTimeClamped;
            public bool IsTimeClamped {
                get { return _isTimeClamped; }
                set {
                    if (value == _isTimeClamped) return;
                    _isTimeClamped = value;
                    OnPropertyChanged();
                }
            }

            private bool _isTemperatureClamped;
            public bool IsTemperatureClamped {
                get { return _isTemperatureClamped; }
                set {
                    if (value == _isTemperatureClamped) return;
                    _isTemperatureClamped = value;
                    OnPropertyChanged();
                }
            }

            private bool _isWeatherNotSupported;
            public bool IsWeatherNotSupported {
                get { return _isWeatherNotSupported; }
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

                    var address = Prompt.Show("Where are you?", "Local Address", localAddress);
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
                get { return _temperature; }
                set {
                    value = value.Round(0.5);
                    if (Equals(value, _temperature)) return;
                    _temperature = value.Clamp(CommonAcConsts.TemperatureMinimum,
                            SettingsHolder.Drive.QuickDriveExpandBounds ? CommonAcConsts.TemperatureMaximum * 2 : CommonAcConsts.TemperatureMaximum);
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    } else {
                        SaveLater();
                    }
                }
            }

            private DelegateCommand _randomTemperatureCommand;

            public DelegateCommand RandomTemperatureCommand => _randomTemperatureCommand ?? (_randomTemperatureCommand = new DelegateCommand(() => {
                RealConditions = false;
                Temperature = MathUtils.Random(CommonAcConsts.TemperatureMinimum, CommonAcConsts.TemperatureMaximum);
            }));

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeather?.TemperatureCoefficient ?? 0.0);

            private int _time;

            public int Time {
                get { return _time; }
                set {
                    if (value == _time) return;
                    _time = value.Clamp(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
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

            private DelegateCommand _randomTimeCommand;

            public DelegateCommand RandomTimeCommand => _randomTimeCommand ?? (_randomTimeCommand = new DelegateCommand(() => {
                if (RealConditions) {
                    RealConditionsManualTime = true;
                }

                Time = MathUtils.Random(CommonAcConsts.TimeMinimum, CommonAcConsts.TimeMaximum);
            }));

            public string DisplayTime {
                get { return $"{_time / 60 / 60:D2}:{_time / 60 % 60:D2}"; }
                set {
                    int time;
                    if (!FlexibleParser.TryParseTime(value, out time)) return;
                    Time = time;
                }
            }

            private CancellationTokenSource _updateCancellationTokenSource;

            public async void UpdateConditions() {
                if (_saveable.IsLoading) return;
                _updateCancellationTokenSource?.Cancel();

                if (!RealConditions) {
                    IsTemperatureClamped = false;
                    IsWeatherNotSupported = false;
                    ManualTime = true;
                    RealWeather = null;
                    _weatherTypeHelper.Reset();
                } else {
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

                var weather = SelectedWeather;
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
                    weather = candidates.RandomElement();
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