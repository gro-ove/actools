using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
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
            #region Constants and other non-changeable values
            public AcEnabledOnlyCollection<WeatherObject> WeatherList { get; } = WeatherManager.Instance.EnabledOnlyCollection;

            private const int SecondsPerDay = 24 * 60 * 60;
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

            public ICommand SwitchLocalWeatherCommand => _switchLocalWeatherCommand ?? (_switchLocalWeatherCommand = new AsyncCommand(async o => {
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
                    _temperature = value.Clamp(TemperatureMinimum, SettingsHolder.Drive.QuickDriveExpandBounds ? TemperatureMaximum * 2 : TemperatureMaximum);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RoadTemperature));

                    if (RealConditions) {
                        TryToSetWeatherLater();
                    } else {
                        SaveLater();
                    }
                }
            }

            public double RoadTemperature => Game.ConditionProperties.GetRoadTemperature(Time, Temperature,
                    SelectedWeather?.TemperatureCoefficient ?? 0.0);

            private int _time;

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

            private CancellationTokenSource _updateCancellationTokenSource;
            public async void UpdateConditions() {
                if (_saveable.IsLoading) return;
                _updateCancellationTokenSource?.Cancel();

                if (!RealConditions) {
                    IsTemperatureClamped = false;
                    IsWeatherNotSupported = false;
                    ManualTime = true;
                    RealWeather = null;

                    _weatherCandidatesFootprint = null;
                } else {
                    ManualTime = RealConditionsManualTime;

                    using (var cancellation = new CancellationTokenSource()) {
                        _updateCancellationTokenSource = cancellation;

                        try {
                            await Update(cancellation.Token);
                        } catch (TaskCanceledException) { } catch (Exception e) {
                            Logging.Warning("[QuickDrive.Conditions] Update(): " + e);
                        }

                        _updateCancellationTokenSource = null;
                    }
                }
            }

            private void TrackUpdated() {
                UpdateConditions();
            }

            private async Task Update(CancellationToken cancellation) {
                GeoTagsEntry trackGeoTags = null, localGeoTags = null;

                if (!RealConditionsLocalWeather || RealConditionsTimezones) {
                    var track = SelectedTrack;
                    trackGeoTags = track.GeoTags;
                    if (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid) {
                        trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                        if (cancellation.IsCancellationRequested) return;
                    }
                }

                if ((trackGeoTags == null || RealConditionsLocalWeather) && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                    localGeoTags = await TracksLocator.TryToLocateAsync(SettingsHolder.Drive.LocalAddress);
                    if (cancellation.IsCancellationRequested) return;
                }

                // Time
                var now = DateTime.Now;
                var time = now.Hour * 60 * 60 + now.Minute * 60 + now.Second;

                if (!RealConditionsManualTime) {
                    if (trackGeoTags == null || !RealConditionsTimezones) {
                        TryToSetTime(time);
                    } else {
                        var timeZone = await TimeZoneDeterminer.TryToDetermineAsync(trackGeoTags);
                        if (cancellation.IsCancellationRequested) return;

                        TryToSetTime((time + (int)(timeZone == null ? 0 : timeZone.BaseUtcOffset.TotalSeconds - TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds) +
                                SecondsPerDay) % SecondsPerDay);
                    }
                }

                // Weather
                var tags = RealConditionsLocalWeather ? localGeoTags : trackGeoTags ?? localGeoTags;
                if (tags == null) return;

                var weather = await WeatherProvider.TryToGetWeatherAsync(tags);
                if (cancellation.IsCancellationRequested) return;

                if (weather != null) {
                    RealWeather = weather;
                    
                    TryToSetTemperature(weather.Temperature);
                    SelectedWeatherType = weather.Type;
                    TryToSetWeather();
                }
            }

            private void TryToSetTime(int value) {
                var clamped = value.Clamp((int)TimeMinimum, (int)TimeMaximum);
                IsTimeClamped = clamped != value;
                Time = clamped;
            }

            private void TryToSetTemperature(double value) {
                var clamped = value.Clamp(TemperatureMinimum, TemperatureMaximum);
                IsTemperatureClamped = value < TemperatureMinimum || value > TemperatureMaximum;
                Temperature = clamped;
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
}