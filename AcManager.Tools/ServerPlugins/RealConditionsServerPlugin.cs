using System;
using System.Diagnostics;
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
using JetBrains.Annotations;
using SystemHalf;

namespace AcManager.Tools.ServerPlugins {
    public class RealConditionsServerPlugin : AcServerPlugin {
        private static readonly TimeSpan UpdatePeriod = TimeSpan.FromSeconds(30d);
        private static readonly TimeSpan UpdateRainPeriod = TimeSpan.FromSeconds(0.5d);
        private static readonly TimeSpan UpdateWeatherPeriod = TimeSpan.FromMinutes(30d);

        private readonly bool _useV2;
        private bool _disposed;

        public RealConditionsServerPlugin([CanBeNull] TrackObjectBase track, bool useV2) {
            _useV2 = useV2;
            SyncWeatherAsync(track).Ignore();
            SimulateRainAsync().Ignore();
        }

        public override void OnInit() {
            base.OnInit();
            BroadcastLoopAsync().Ignore();
        }

        public override void Dispose() {
            _disposed = true;
        }

        private string GetSerializedCspCommand([NotNull] WeatherDescription current, [NotNull] WeatherDescription next) {
            var transition = GetWeatherTransition();
            var date = _startingDate + _timePassedTotal.Elapsed;
            // var date = _startingDate + TimeSpan.FromSeconds(_timePassedTotal.Elapsed.TotalSeconds * 4000);
            var ambientTemperature = transition.Lerp(current.Temperature, next.Temperature);
            var roadTemperature = Game.ConditionProperties.GetRoadTemperature(date.TimeOfDay.TotalSeconds.RoundToInt(), ambientTemperature,
                    transition.Lerp(GetRoadTemperatureCoefficient(current.Type), GetRoadTemperatureCoefficient(next.Type)));
            var windSpeed = transition.Lerp(current.WindSpeed, next.WindSpeed) * 3.6;

            // Second version would affect conditions physics, so it’s optional, active only if server requires CSP v1643 or newer
            return _useV2
                    ? new CommandWeatherSetV2 {
                        Timestamp = (ulong)date.ToUnixTimestamp(),
                        TimeToApply = (Half)UpdatePeriod.TotalSeconds,
                        WeatherCurrent = (CommandWeatherType)current.Type,
                        WeatherNext = (CommandWeatherType)next.Type,
                        Transition = (ushort)(65535 * transition),
                        WindSpeedKmh = (Half)windSpeed,
                        WindDirectionDeg = (Half)transition.Lerp(current.WindDirection, next.WindDirection),
                        Humidity = (byte)(transition.Lerp(current.Humidity, next.Humidity) * 255d),
                        Pressure = (Half)transition.Lerp(current.Pressure, next.Pressure),
                        TemperatureAmbient = (Half)ambientTemperature,
                        TemperatureRoad = (Half)roadTemperature,
                        TrackGrip = 99d,
                        RainIntensity = (Half)_rainIntensity,
                        RainWetness = (Half)_rainWetness,
                        RainWater = (Half)_rainWater
                    }.Serialize()
                    : new CommandWeatherSetV1 {
                        Timestamp = (ulong)date.ToUnixTimestamp(),
                        TimeToApply = (float)UpdatePeriod.TotalSeconds,
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
                await Task.Delay(UpdatePeriod);
            }
        }

        // private readonly DateTime _startingDate = DateTime.Now + TimeSpan.FromHours(12d);
        private readonly DateTime _startingDate = DateTime.Now;
        private readonly Stopwatch _timePassedTotal = Stopwatch.StartNew();
        private readonly Stopwatch _weatherTransitionStopwatch = Stopwatch.StartNew();

        [CanBeNull]
        private WeatherDescription _weatherCurrent, _weatherNext;

        private double _rainIntensity;
        private double _rainWetness;
        private double _rainWater;

        private double GetWeatherTransition() {
            return (_weatherTransitionStopwatch.Elapsed.TotalSeconds / UpdateWeatherPeriod.TotalSeconds).Saturate().SmoothStep();
        }

        private async Task SimulateRainAsync() {
            while (!_disposed) {
                var current = _weatherCurrent;
                var next = _weatherNext;
                _rainIntensity = current == null || next == null ? 0d : GetWeatherTransition().Lerp(GetRainIntensity(current.Type), GetRainIntensity(next.Type));
                _rainWetness += ((_rainIntensity > 0 ? 1d : 0d) - _rainWetness) * (_rainIntensity > 0 ? _rainIntensity.Lerp(0.002, 0.01) : 0.005);
                _rainWater += (_rainIntensity - _rainWater) * 0.005;
                await Task.Delay(UpdateRainPeriod);
            }
        }

        private void ApplyWeather([CanBeNull] WeatherDescription weather) {
            Logging.Warning("New weather type: " + weather?.Type);
            if (weather == null) {
                return;
            }

            _weatherTransitionStopwatch.Restart();
            _weatherCurrent = _weatherNext ?? weather;
            _weatherNext = weather;
        }

        private async Task SyncWeatherAsync([CanBeNull] TrackObjectBase track) {
            var trackGeoTags = track?.GeoTags;
            if (track != null && (trackGeoTags == null || trackGeoTags.IsEmptyOrInvalid)) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(track);
                if (_disposed) return;
            }

            if (trackGeoTags == null && !string.IsNullOrWhiteSpace(SettingsHolder.Drive.LocalAddress)) {
                trackGeoTags = await TracksLocator.TryToLocateAsync(SettingsHolder.Drive.LocalAddress);
                if (_disposed) return;
            }

            if (trackGeoTags == null) {
                Logging.Warning("Failed to calculate coordinates for real conditions");
                Dispose();
                return;
            }

            Logging.Debug("Geotags: " + trackGeoTags);
            while (!_disposed) {
                ApplyWeather(await WeatherProvider.TryToGetWeatherAsync(trackGeoTags));
                await Task.Delay(UpdateWeatherPeriod);
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
                    return 0.7;
                case WeatherType.Thunderstorm:
                    return 0.2;
                case WeatherType.HeavyThunderstorm:
                    return -0.2;
                case WeatherType.LightDrizzle:
                    return 0.1;
                case WeatherType.Drizzle:
                    return -0.1;
                case WeatherType.HeavyDrizzle:
                    return -0.3;
                case WeatherType.LightRain:
                    return 0.01;
                case WeatherType.Rain:
                    return -0.2;
                case WeatherType.HeavyRain:
                    return -0.5;
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