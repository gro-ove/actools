using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Messages;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SystemHalf;

namespace AcManager.Tools.ServerPlugins {
    public class LiveConditionsServerPlugin : AcServerPlugin {
        [JsonObject(MemberSerialization.OptIn)]
        public class LiveConditionParams : NotifyPropertyChanged {
            private bool _useRealConditions = true;

            [JsonProperty("useRealConditions")]
            public bool UseRealConditions {
                get => _useRealConditions;
                set => Apply(value, ref _useRealConditions);
            }

            private TimeSpan _timeOffset;

            [JsonProperty("timeOffset")]
            public TimeSpan TimeOffset {
                get => _timeOffset;
                set => Apply(value, ref _timeOffset, () => OnPropertyChanged(nameof(TimeOffsetHours)));
            }

            public double TimeOffsetHours {
                get => _timeOffset.TotalHours;
                set => TimeOffset = TimeSpan.FromHours(value);
            }

            private bool _useFixedStartingTime;

            [JsonProperty("useFixedStartingTime")]
            public bool UseFixedStartingTime {
                get => _useFixedStartingTime;
                set => Apply(value, ref _useFixedStartingTime);
            }

            private int _fixedStartingTimeValue = 12 * 60 * 60;

            [JsonProperty("fixedStartingTime")]
            public int FixedStartingTimeValue {
                get => _fixedStartingTimeValue;
                set => Apply(value, ref _fixedStartingTimeValue, () => OnPropertyChanged(nameof(DisplayTime)));
            }

            public string DisplayTime {
                get => _fixedStartingTimeValue.ToDisplayTime();
                set {
                    if (!FlexibleParser.TryParseTime(value, out var time)) return;
                    FixedStartingTimeValue = time;
                }
            }

            private DateTime _fixedStartingDateValue = DateTime.Now;

            [JsonProperty("fixedStartingDate")]
            public DateTime FixedStartingDateValue {
                get => _fixedStartingDateValue;
                set {
                    if (value < 0L.ToDateTime()) value = DateTime.Now;
                    Apply(value, ref _fixedStartingDateValue);
                }
            }

            private double _timeMultiplier = 1d;

            [JsonProperty("timeMultiplier")]
            public double TimeMultiplier {
                get => _timeMultiplier;
                set => Apply(value, ref _timeMultiplier);
            }

            private double _temperatureOffset;

            [JsonProperty("temperatureOffset")]
            public double TemperatureOffset {
                get => _temperatureOffset;
                set => Apply(value, ref _temperatureOffset, () => OnPropertyChanged(nameof(DisplayEstimatedTemperature)));
            }

            public string DisplayEstimatedTemperature => $"Approximate temperature range: {MinTemperature + TemperatureOffset}…{MaxTemperature + TemperatureOffset} °C";

            private bool _useFixedAirTemperature;

            [JsonProperty("useFixedAirTemperature")]
            public bool UseFixedAirTemperature {
                get => _useFixedAirTemperature;
                set => Apply(value, ref _useFixedAirTemperature);
            }

            private double _fixedAirTemperature = 25d;

            [JsonProperty("fixedAirTemperature")]
            public double FixedAirTemperature {
                get => _fixedAirTemperature;
                set => Apply(value, ref _fixedAirTemperature);
            }

            private TimeSpan _weatherTypeChangePeriod = TimeSpan.FromMinutes(5d);

            [JsonProperty("weatherTypeChangePeriod")]
            public TimeSpan WeatherTypeChangePeriod {
                get => _weatherTypeChangePeriod;
                set => Apply(value, ref _weatherTypeChangePeriod, () => OnPropertyChanged(nameof(WeatherTypeChangeMinutes)));
            }

            public double WeatherTypeChangeMinutes {
                get => _weatherTypeChangePeriod.TotalMinutes;
                set => WeatherTypeChangePeriod = TimeSpan.FromMinutes(value);
            }

            private bool _weatherTypeChangeToNeighboursOnly = true;

            [JsonProperty("weatherTypeChangeToNeighboursOnly")]
            public bool WeatherTypeChangeToNeighboursOnly {
                get => _weatherTypeChangeToNeighboursOnly;
                set => Apply(value, ref _weatherTypeChangeToNeighboursOnly);
            }

            private double _weatherRainChance = 0.05;

            [JsonProperty("weatherRainChance")]
            public double WeatherRainChance {
                get => _weatherRainChance;
                set => Apply(value, ref _weatherRainChance);
            }

            private double _weatherThunderChance = 0.005;

            [JsonProperty("weatherThunderChance")]
            public double WeatherThunderChance {
                get => _weatherThunderChance;
                set => Apply(value, ref _weatherThunderChance);
            }

            private double _trackGripStartingValue = 99d;

            [JsonProperty("startingTrackGrip")]
            public double TrackGripStartingValue {
                get => _trackGripStartingValue;
                set => Apply(value, ref _trackGripStartingValue);
            }

            private double _trackGripIncreasePerLap = 0.05d;

            [JsonProperty("trackGripIncreasePerLap")]
            public double TrackGripIncreasePerLap {
                get => _trackGripIncreasePerLap;
                set => Apply(value, ref _trackGripIncreasePerLap);
            }

            private double _trackGripTransfer = 80d;

            [JsonProperty("trackGripTransfer")]
            public double TrackGripTransfer {
                get => _trackGripTransfer;
                set => Apply(value, ref _trackGripTransfer);
            }

            private double _rainTimeMultiplier = 1d;

            [JsonProperty("rainTimeMultiplier")]
            public double RainTimeMultiplier {
                get => _rainTimeMultiplier;
                set => Apply(value, ref _rainTimeMultiplier);
            }

            private TimeSpan _rainWetnessIncreaseTime = TimeSpan.FromMinutes(3d);

            [JsonProperty("rainWetnessIncreaseTime")]
            public TimeSpan RainWetnessIncreaseTime {
                get => _rainWetnessIncreaseTime;
                set => Apply(value, ref _rainWetnessIncreaseTime, () => OnPropertyChanged(nameof(RainWetnessIncreaseMinutes)));
            }

            public double RainWetnessIncreaseMinutes {
                get => _rainWetnessIncreaseTime.TotalMinutes;
                set => RainWetnessIncreaseTime = TimeSpan.FromMinutes(value);
            }

            private TimeSpan _rainWetnessDecreaseTime = TimeSpan.FromMinutes(15d);

            [JsonProperty("rainWetnessDecreaseTime")]
            public TimeSpan RainWetnessDecreaseTime {
                get => _rainWetnessDecreaseTime;
                set => Apply(value, ref _rainWetnessDecreaseTime, () => OnPropertyChanged(nameof(RainWetnessDecreaseMinutes)));
            }

            public double RainWetnessDecreaseMinutes {
                get => _rainWetnessDecreaseTime.TotalMinutes;
                set => RainWetnessDecreaseTime = TimeSpan.FromMinutes(value);
            }

            private TimeSpan _rainWaterIncreaseTime = TimeSpan.FromMinutes(30d);

            [JsonProperty("rainWaterIncreaseTime")]
            public TimeSpan RainWaterIncreaseTime {
                get => _rainWaterIncreaseTime;
                set => Apply(value, ref _rainWaterIncreaseTime, () => OnPropertyChanged(nameof(RainWaterIncreaseMinutes)));
            }

            public double RainWaterIncreaseMinutes {
                get => _rainWaterIncreaseTime.TotalMinutes;
                set => RainWaterIncreaseTime = TimeSpan.FromMinutes(value);
            }

            private TimeSpan _rainWaterDecreaseTime = TimeSpan.FromMinutes(120d);

            [JsonProperty("rainWaterDecreaseTime")]
            public TimeSpan RainWaterDecreaseTime {
                get => _rainWaterDecreaseTime;
                set => Apply(value, ref _rainWaterDecreaseTime, () => OnPropertyChanged(nameof(RainWaterDecreaseMinutes)));
            }

            public double RainWaterDecreaseMinutes {
                get => _rainWaterDecreaseTime.TotalMinutes;
                set => RainWaterDecreaseTime = TimeSpan.FromMinutes(value);
            }

            public string Serialize() {
                return JsonConvert.SerializeObject(this).ToCutBase64();
            }

            public void Deserialize(string data) {
                if (string.IsNullOrWhiteSpace(data)) return;
                try {
                    JsonConvert.PopulateObject(Encoding.UTF8.GetString(data.FromCutBase64() ?? new byte[0]), this);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
                if (_trackGripIncreasePerLap == 0d) {
                    NonfatalError.Notify("Failed to load dynamic conditions configuration");
                }
            }

            public LiveConditionParams Clone() {
                var created = new LiveConditionParams();
                created.Deserialize(Serialize());
                return created;
            }
        }

        private static readonly TimeSpan UpdateRainPeriod = TimeSpan.FromSeconds(0.5d);
        private static readonly TimeSpan UpdateWeatherPeriod = TimeSpan.FromMinutes(10d);

        [CanBeNull]
        private readonly TrackObjectBase _track;

        private readonly bool _useV2;
        private readonly LiveConditionParams _liveParams;
        private TimeSpan _updatePeriod;
        private TimeSpan _broadcastPeriod;
        private double _drivenLapsEstimate;
        private double _lapLengthKm;
        private bool _disposed;

        public LiveConditionsServerPlugin([CanBeNull] TrackObjectBase track, bool useV2, LiveConditionParams liveParams) {
            _track = track;
            _useV2 = useV2;
            _liveParams = liveParams;
            _lapLengthKm = track?.SpecsLengthValue / 1e3 ?? 0d;
            _startingDate = DateTime.Now;
            _broadcastPeriod = liveParams.UseRealConditions ? TimeSpan.FromMinutes(1d)
                    : TimeSpan.FromMinutes((liveParams.WeatherTypeChangePeriod.TotalMinutes * 0.2).Clamp(0.1, 2d));
            if (_lapLengthKm < 1) _lapLengthKm = 4;
            InitAsync().Ignore();
        }

        public override void OnInit() {
            base.OnInit();
            BroadcastLoopAsync().Ignore();
        }

        public override void Dispose() {
            _disposed = true;
        }

        public override void OnNewSession(MsgSessionInfo msg) {
            base.OnNewSession(msg);
            _drivenLapsEstimate *= (_liveParams.TrackGripTransfer / 100d).Saturate();
        }

        private async Task InitAsync() {
            _startingDate = _liveParams.UseFixedStartingTime
                    ? _liveParams.FixedStartingDateValue.Date + TimeSpan.FromSeconds(_liveParams.FixedStartingTimeValue)
                            - await RealConditionsHelper.GetTimezoneOffsetAsync(_track, CancellationToken.None)
                    : DateTime.Now + _liveParams.TimeOffset;
            SyncWeatherAsync(_track).Ignore();
            UpdateStateAsync().Ignore();
        }

        private string GetSerializedCspCommand([NotNull] WeatherDescription current, [NotNull] WeatherDescription next) {
            var transition = GetWeatherTransition();
            var date = _startingDate + TimeSpan.FromSeconds(_timePassedTotal.Elapsed.TotalSeconds * _liveParams.TimeMultiplier);
            var ambientTemperature = _liveParams.UseFixedAirTemperature
                    ? _liveParams.FixedAirTemperature : transition.Lerp(current.Temperature, next.Temperature) + _liveParams.TemperatureOffset;
            var roadTemperature = Game.ConditionProperties.GetRoadTemperature(date.TimeOfDay.TotalSeconds.RoundToInt(), ambientTemperature,
                    transition.Lerp(GetRoadTemperatureCoefficient(current.Type), GetRoadTemperatureCoefficient(next.Type)));
            var windSpeed = transition.Lerp(current.WindSpeed, next.WindSpeed) * 3.6;
            var grip = ((_liveParams.TrackGripStartingValue + _liveParams.TrackGripIncreasePerLap * _drivenLapsEstimate) / 100d).Clamp(0.6, 1d);

            // Second version would affect conditions physics, so it’s optional, active only if server requires CSP v1643 or newer
            return _useV2
                    ? new CommandWeatherSetV2 {
                        Timestamp = (ulong)date.ToUnixTimestamp(),
                        TimeToApply = (Half)_broadcastPeriod.TotalSeconds,
                        WeatherCurrent = (CommandWeatherType)current.Type,
                        WeatherNext = (CommandWeatherType)next.Type,
                        Transition = (ushort)(65535 * transition),
                        WindSpeedKmh = (Half)windSpeed,
                        WindDirectionDeg = (Half)transition.Lerp(current.WindDirection, next.WindDirection),
                        Humidity = (byte)(transition.Lerp(current.Humidity, next.Humidity) * 255d),
                        Pressure = (Half)transition.Lerp(current.Pressure, next.Pressure),
                        TemperatureAmbient = (Half)ambientTemperature,
                        TemperatureRoad = (Half)roadTemperature,
                        TrackGrip = grip,
                        RainIntensity = (Half)_rainIntensity,
                        RainWetness = (Half)_rainWetness,
                        RainWater = (Half)_rainWater.LerpInvSat(0.1, 1)
                    }.Serialize()
                    : new CommandWeatherSetV1 {
                        Timestamp = (ulong)date.ToUnixTimestamp(),
                        TimeToApply = (float)_broadcastPeriod.TotalSeconds,
                        WeatherCurrent = (CommandWeatherType)current.Type,
                        WeatherNext = (CommandWeatherType)next.Type,
                        Transition = (float)transition,
                    }.Serialize();
        }

        public override void OnClientLoaded(MsgClientLoaded msg) {
            // Sending conditions directly to all newly connected:
            if (_weatherCurrent != null && _weatherNext != null) {
                PluginManager.SendCspCommand(msg.CarId, GetSerializedCspCommand(_weatherCurrent, _weatherNext));
            }
        }

        private async Task BroadcastLoopAsync() {
            while (!_disposed) {
                if (_weatherCurrent != null && _weatherNext != null && PluginManager.IsConnected) {
                    PluginManager.BroadcastCspCommand(GetSerializedCspCommand(_weatherCurrent, _weatherNext));
                }
                await Task.Delay(_broadcastPeriod);
            }
        }

        private readonly Stopwatch _timePassedTotal = Stopwatch.StartNew();
        private readonly Stopwatch _weatherTransitionStopwatch = Stopwatch.StartNew();

        [CanBeNull]
        private WeatherDescription _weatherCurrent, _weatherNext;

        private double _rainCurrent, _rainNext;

        private DateTime _startingDate;
        private double _rainIntensity;
        private double _rainWetness;
        private double _rainWater;

        private double GetWeatherTransition() {
            return (_weatherTransitionStopwatch.Elapsed.TotalSeconds / _updatePeriod.TotalSeconds).Saturate().SmoothStep();
        }

        private async Task UpdateStateAsync() {
            while (!_disposed) {
                var transition = GetWeatherTransition();

                var current = _weatherCurrent;
                var next = _weatherNext;
                var weatherMissing = current == null || next == null;
                _rainIntensity = weatherMissing ? 0d : transition.Lerp(_rainCurrent, _rainNext);

                var date = _startingDate + TimeSpan.FromSeconds(_timePassedTotal.Elapsed.TotalSeconds * _liveParams.TimeMultiplier);
                var ambientTemperature = weatherMissing ? 25d : _liveParams.UseFixedAirTemperature
                        ? _liveParams.FixedAirTemperature : transition.Lerp(current.Temperature, next.Temperature) + _liveParams.TemperatureOffset;
                var roadTemperature = Game.ConditionProperties.GetRoadTemperature(date.TimeOfDay.TotalSeconds.RoundToInt(), ambientTemperature,
                        transition.Lerp(
                                GetRoadTemperatureCoefficient(current?.Type ?? WeatherType.Clear),
                                GetRoadTemperatureCoefficient(next?.Type ?? WeatherType.Clear)));

                if (_rainIntensity > 0d) {
                    _rainWetness = Math.Min(1d, _rainWetness + _rainIntensity.Lerp(0.3, 1.7d)
                            * UpdateRainPeriod.TotalSeconds / Math.Max(1d, _liveParams.RainWetnessIncreaseTime.TotalSeconds * _liveParams.RainTimeMultiplier));
                } else {
                    _rainWetness = Math.Max(0d, _rainWetness - roadTemperature.LerpInvSat(10d, 35d).Lerp(0.3, 1.7d)
                            * UpdateRainPeriod.TotalSeconds / Math.Max(1d, _liveParams.RainWetnessDecreaseTime.TotalSeconds * _liveParams.RainTimeMultiplier));
                }

                if (_rainWater < _rainIntensity) {
                    _rainWater = Math.Min(_rainIntensity, _rainWater + _rainIntensity.Lerp(0.3, 1.7d)
                            * UpdateRainPeriod.TotalSeconds / Math.Max(1d, _liveParams.RainWaterIncreaseTime.TotalSeconds * _liveParams.RainTimeMultiplier));
                } else {
                    _rainWater = Math.Max(_rainIntensity, _rainWater - roadTemperature.LerpInvSat(10d, 35d).Lerp(0.3, 1.7d)
                            * UpdateRainPeriod.TotalSeconds / Math.Max(1d, _liveParams.RainWaterDecreaseTime.TotalSeconds * _liveParams.RainTimeMultiplier));
                }

                if (PluginManager != null) {
                    foreach (var info in PluginManager.GetDriverInfos()) {
                        if (info?.IsConnected == true) {
                            var drivenDistanceKm = info.CurrentSpeed * UpdateRainPeriod.TotalHours;
                            _drivenLapsEstimate += drivenDistanceKm / _lapLengthKm;
                        }
                    }
                }

                await Task.Delay(UpdateRainPeriod);
            }
        }

        private void ApplyWeather([CanBeNull] WeatherDescription weather) {
            Logging.Warning("New weather type: " + weather?.Type);
            if (weather == null) {
                return;
            }

            var rain = GetRainIntensity(weather.Type) * MathUtils.Random(0.8, 1.2);
            _weatherTransitionStopwatch.Restart();
            _rainCurrent = _weatherNext != null ? _rainNext : rain;
            _rainNext = rain;
            _weatherCurrent = _weatherNext ?? weather;
            _weatherNext = weather;
        }

        private static readonly Dictionary<WeatherType, List<WeatherType>> Neighbours = new Dictionary<WeatherType, List<WeatherType>>();

        static LiveConditionsServerPlugin() {
            void AddConnection(WeatherType a, WeatherType b) {
                if (!Neighbours.ContainsKey(a)) Neighbours[a] = new List<WeatherType>();
                if (!Neighbours.ContainsKey(b)) Neighbours[b] = new List<WeatherType>();
                Neighbours[a].Add(b);
                Neighbours[b].Add(a);
            }

            AddConnection(WeatherType.Clear, WeatherType.FewClouds);
            AddConnection(WeatherType.Clear, WeatherType.ScatteredClouds);
            AddConnection(WeatherType.Clear, WeatherType.Mist);
            AddConnection(WeatherType.FewClouds, WeatherType.ScatteredClouds);
            AddConnection(WeatherType.FewClouds, WeatherType.BrokenClouds);
            AddConnection(WeatherType.FewClouds, WeatherType.Mist);
            AddConnection(WeatherType.FewClouds, WeatherType.Windy);
            AddConnection(WeatherType.FewClouds, WeatherType.LightDrizzle);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.BrokenClouds);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.OvercastClouds);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.Mist);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.Windy);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.Drizzle);
            AddConnection(WeatherType.ScatteredClouds, WeatherType.LightRain);
            AddConnection(WeatherType.BrokenClouds, WeatherType.OvercastClouds);
            AddConnection(WeatherType.BrokenClouds, WeatherType.Mist);
            AddConnection(WeatherType.BrokenClouds, WeatherType.Windy);
            AddConnection(WeatherType.BrokenClouds, WeatherType.Drizzle);
            AddConnection(WeatherType.BrokenClouds, WeatherType.Rain);
            AddConnection(WeatherType.OvercastClouds, WeatherType.Mist);
            AddConnection(WeatherType.OvercastClouds, WeatherType.Fog);
            AddConnection(WeatherType.OvercastClouds, WeatherType.Windy);
            AddConnection(WeatherType.OvercastClouds, WeatherType.HeavyDrizzle);
            AddConnection(WeatherType.OvercastClouds, WeatherType.Rain);
            AddConnection(WeatherType.Fog, WeatherType.Mist);
            AddConnection(WeatherType.Fog, WeatherType.Rain);
            AddConnection(WeatherType.Mist, WeatherType.LightDrizzle);
            AddConnection(WeatherType.Mist, WeatherType.LightRain);
            AddConnection(WeatherType.Mist, WeatherType.Windy);
            AddConnection(WeatherType.Windy, WeatherType.Drizzle);
            AddConnection(WeatherType.Windy, WeatherType.LightRain);
            AddConnection(WeatherType.Windy, WeatherType.Rain);
            AddConnection(WeatherType.LightDrizzle, WeatherType.Drizzle);
            AddConnection(WeatherType.LightDrizzle, WeatherType.HeavyDrizzle);
            AddConnection(WeatherType.LightDrizzle, WeatherType.LightRain);
            AddConnection(WeatherType.LightDrizzle, WeatherType.Rain);
            AddConnection(WeatherType.Drizzle, WeatherType.HeavyDrizzle);
            AddConnection(WeatherType.Drizzle, WeatherType.LightRain);
            AddConnection(WeatherType.Drizzle, WeatherType.Rain);
            AddConnection(WeatherType.HeavyDrizzle, WeatherType.LightRain);
            AddConnection(WeatherType.HeavyDrizzle, WeatherType.Rain);
            AddConnection(WeatherType.HeavyDrizzle, WeatherType.HeavyRain);
            AddConnection(WeatherType.HeavyDrizzle, WeatherType.LightThunderstorm);
            AddConnection(WeatherType.LightRain, WeatherType.Rain);
            AddConnection(WeatherType.LightRain, WeatherType.LightThunderstorm);
            AddConnection(WeatherType.Rain, WeatherType.HeavyRain);
            AddConnection(WeatherType.Rain, WeatherType.Thunderstorm);
            AddConnection(WeatherType.HeavyRain, WeatherType.Thunderstorm);
            AddConnection(WeatherType.HeavyRain, WeatherType.HeavyThunderstorm);
            AddConnection(WeatherType.LightThunderstorm, WeatherType.Thunderstorm);
            AddConnection(WeatherType.Thunderstorm, WeatherType.HeavyThunderstorm);

            foreach (var neighbour in Neighbours) {
                neighbour.Value.Add(neighbour.Key);
            }
        }

        private static readonly List<Tuple<double, WeatherType>> ChancesRegular = new List<Tuple<double, WeatherType>> {
            Tuple.Create(0.4, WeatherType.Clear),
            Tuple.Create(0.2, WeatherType.FewClouds),
            Tuple.Create(0.1, WeatherType.ScatteredClouds),
            Tuple.Create(0.1, WeatherType.BrokenClouds),
            Tuple.Create(0.2, WeatherType.OvercastClouds),
            Tuple.Create(0.1, WeatherType.Fog),
            Tuple.Create(0.1, WeatherType.Mist),
            Tuple.Create(0.2, WeatherType.Windy),
        };

        private static readonly List<Tuple<double, WeatherType>> ChancesRain = new List<Tuple<double, WeatherType>> {
            Tuple.Create(0.1, WeatherType.LightDrizzle),
            Tuple.Create(0.2, WeatherType.Drizzle),
            Tuple.Create(0.2, WeatherType.HeavyDrizzle),
            Tuple.Create(0.2, WeatherType.LightRain),
            Tuple.Create(0.3, WeatherType.Rain),
            Tuple.Create(0.1, WeatherType.HeavyRain),
        };

        private static readonly List<Tuple<double, WeatherType>> ChancesThunderstorm = new List<Tuple<double, WeatherType>> {
            Tuple.Create(0.6, WeatherType.LightThunderstorm),
            Tuple.Create(0.3, WeatherType.Thunderstorm),
            Tuple.Create(0.1, WeatherType.HeavyThunderstorm),
        };

        private WeatherType? PickRandomWeatherType(List<Tuple<double, WeatherType>> table, [CanBeNull] List<WeatherType> allowed) {
            if (allowed != null) {
                table = table.Where(x => allowed.Contains(x.Item2)).ToList();
            }
            if (table.Count == 0) return null;
            var chance = MathUtils.Random() * table.Sum(x => x.Item1);
            foreach (var item in table) {
                if (chance < item.Item1) return item.Item2;
                chance -= item.Item1;
            }
            return table.FirstOrDefault()?.Item2;
        }

        private WeatherType GenerateRandomWeatherType(WeatherType? currentType) {
            if (!_liveParams.WeatherTypeChangeToNeighboursOnly && currentType.HasValue && MathUtils.Random() < 0.2) {
                return currentType.Value;
            }

            var allowed = currentType.HasValue && _liveParams.WeatherTypeChangeToNeighboursOnly ? Neighbours.GetValueOrDefault(currentType.Value) : null;
            var chance = MathUtils.Random() * Math.Max(1, _liveParams.WeatherThunderChance + _liveParams.WeatherRainChance) - _liveParams.WeatherThunderChance;
            if (chance < 0 || MathUtils.Random() > 0.2 && ChancesThunderstorm.Any(x => x.Item2 == currentType)) {
                var type = PickRandomWeatherType(ChancesThunderstorm, allowed);
                if (type.HasValue) {
                    return type.Value;
                }
            }
            if (chance < _liveParams.WeatherRainChance || MathUtils.Random() > 0.2 && ChancesRain.Any(x => x.Item2 == currentType)) {
                var type = PickRandomWeatherType(ChancesRain, allowed);
                if (type.HasValue) {
                    return type.Value;
                }
            }
            return PickRandomWeatherType(ChancesRegular, allowed) ?? WeatherType.Clear;
        }

        private const double MinTemperature = 22d;
        private const double MaxTemperature = 26d;

        private Tuple<double, double, double> EstimateTemperatureWindSpeedHumidity(WeatherType type) {
            if (type == WeatherType.Clear) return Tuple.Create(26d, 1d, 0.6);
            if (type == WeatherType.FewClouds) return Tuple.Create(25d, 2d, 0.6);
            if (type == WeatherType.ScatteredClouds) return Tuple.Create(25d, 3d, 0.6);
            if (type == WeatherType.BrokenClouds) return Tuple.Create(25d, 4d, 0.6);
            if (type == WeatherType.OvercastClouds) return Tuple.Create(24d, 5d, 0.6);
            if (type == WeatherType.Fog) return Tuple.Create(24d, 0d, 0.9);
            if (type == WeatherType.Mist) return Tuple.Create(24d, 0d, 0.8);
            if (type == WeatherType.Windy) return Tuple.Create(24d, 10d, 0.4);
            if (type == WeatherType.LightDrizzle) return Tuple.Create(25d, 2d, 0.7);
            if (type == WeatherType.Drizzle) return Tuple.Create(24d, 3d, 0.7);
            if (type == WeatherType.HeavyDrizzle) return Tuple.Create(23d, 4d, 0.7);
            if (type == WeatherType.LightRain) return Tuple.Create(24d, 4d, 0.8);
            if (type == WeatherType.Rain) return Tuple.Create(23d, 6d, 0.8);
            if (type == WeatherType.HeavyRain) return Tuple.Create(23d, 10d, 0.8);
            if (type == WeatherType.LightThunderstorm) return Tuple.Create(23d, 12d, 0.9);
            if (type == WeatherType.Thunderstorm) return Tuple.Create(22d, 13d, 0.9);
            if (type == WeatherType.HeavyThunderstorm) return Tuple.Create(22d, 14d, 0.9);
            return Tuple.Create(24d, 1d, 0.6);
        }

        private WeatherDescription GenerateRandomDescription(WeatherType? currentType) {
            var type = GenerateRandomWeatherType(currentType);
            Logging.Debug($"Switching from {currentType?.ToString() ?? "?"} to {type}");
            var estimated = EstimateTemperatureWindSpeedHumidity(type);
            return new WeatherDescription(type, estimated.Item1 * MathUtils.Random(0.95, 1.05), string.Empty, estimated.Item2 * MathUtils.Random(0.6, 1.2),
                    MathUtils.Random() * 360, estimated.Item3 * MathUtils.Random(0.6, 1.2), 1030);
        }

        public static async Task<GeoTagsEntry> GetSomeGeoTagsAsync([CanBeNull] TrackObjectBase track) {
            var trackGeoTags = track?.GeoTags;
            if (track != null && (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid)) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(track);
            }

            if (trackGeoTags == null && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(SettingsHolder.Drive.LocalAddress);
            }
            return trackGeoTags;
        }

        private GeoTagsEntry _geoTags;

        private async Task SyncWeatherAsync([CanBeNull] TrackObjectBase track) {
            if (_geoTags == null) {
                _geoTags = await GetSomeGeoTagsAsync(track);
                if (_geoTags == null) {
                    Logging.Warning("Failed to calculate coordinates for real conditions");
                    Dispose();
                    return;
                }
            }

            if (_geoTags == null) {
                return;
            }

            if (_liveParams.UseRealConditions) {
                _updatePeriod = UpdateWeatherPeriod;
                while (!_disposed) {
                    ApplyWeather(await WeatherProvider.TryToGetWeatherAsync(_geoTags));
                    await Task.Delay(UpdateWeatherPeriod);
                }
            } else {
                while (!_disposed) {
                    _updatePeriod = TimeSpan.FromSeconds(_liveParams.WeatherTypeChangePeriod.TotalSeconds * MathUtils.Random(0.5, 1.5));
                    ApplyWeather(GenerateRandomDescription(_weatherNext?.Type));
                    await Task.Delay(_updatePeriod);
                }
            }
        }

        private static double GetRainIntensity(WeatherType type) {
            switch (type) {
                case WeatherType.None:
                    return 0d;
                case WeatherType.LightThunderstorm:
                    return 0.5;
                case WeatherType.Thunderstorm:
                    return 0.6;
                case WeatherType.HeavyThunderstorm:
                    return 0.7;
                case WeatherType.LightDrizzle:
                    return 0.1;
                case WeatherType.Drizzle:
                    return 0.2;
                case WeatherType.HeavyDrizzle:
                    return 0.3;
                case WeatherType.LightRain:
                    return 0.3;
                case WeatherType.Rain:
                    return 0.4;
                case WeatherType.HeavyRain:
                    return 0.5;
                case WeatherType.LightSnow:
                    return 0.2;
                case WeatherType.Snow:
                    return 0.3;
                case WeatherType.HeavySnow:
                    return 0.4;
                case WeatherType.LightSleet:
                    return 0.3;
                case WeatherType.Sleet:
                    return 0.4;
                case WeatherType.HeavySleet:
                    return 0.5;
                case WeatherType.Squalls:
                    return 0.6;
                case WeatherType.Tornado:
                    return 0.5;
                case WeatherType.Hurricane:
                    return 0.6;
                default:
                    return 0d;
            }
        }

        private static double GetRoadTemperatureCoefficient(WeatherType type) {
            switch (type) {
                case WeatherType.None:
                    return 1d;
                case WeatherType.LightThunderstorm:
                    return -0.9;
                case WeatherType.Thunderstorm:
                    return -1d;
                case WeatherType.HeavyThunderstorm:
                    return -1d;
                case WeatherType.LightDrizzle:
                    return -0.3;
                case WeatherType.Drizzle:
                    return -0.4;
                case WeatherType.HeavyDrizzle:
                    return -0.5;
                case WeatherType.LightRain:
                    return -0.6;
                case WeatherType.Rain:
                    return -0.7;
                case WeatherType.HeavyRain:
                    return -0.8;
                case WeatherType.LightSnow:
                    return -0.7;
                case WeatherType.Snow:
                    return -0.8;
                case WeatherType.HeavySnow:
                    return -0.9;
                case WeatherType.LightSleet:
                    return -1d;
                case WeatherType.Sleet:
                    return -1d;
                case WeatherType.HeavySleet:
                    return -1d;
                case WeatherType.Squalls:
                    return -0.5;
                case WeatherType.Tornado:
                    return -0.3;
                case WeatherType.Hurricane:
                    return -0.6;
                case WeatherType.Clear:
                    return 1d;
                case WeatherType.FewClouds:
                    return 1d;
                case WeatherType.ScatteredClouds:
                    return 0.8;
                case WeatherType.BrokenClouds:
                    return 0.1;
                case WeatherType.OvercastClouds:
                    return 0.01;
                case WeatherType.Fog:
                    return -0.3;
                case WeatherType.Mist:
                    return -0.2;
                case WeatherType.Smoke:
                    return -0.2;
                case WeatherType.Haze:
                    return 0.9;
                case WeatherType.Sand:
                    return 1d;
                case WeatherType.Dust:
                    return 1d;
                case WeatherType.Cold:
                    return -0.8;
                case WeatherType.Hot:
                    return 1d;
                case WeatherType.Windy:
                    return 0.3;
                case WeatherType.Hail:
                    return -1d;
                default:
                    return 0d;
            }
        }
    }
}