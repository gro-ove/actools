using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SystemHalf;

namespace AcManager.Tools.ServerPlugins {
    // More advanced version, uses v3 weather description with all conditions. Most important values are fixed or generated randomly and
    // poorly, whole thing is mostly meant for testing.
    public class DynamicConditionsV3ServerPlugin : AcServerPlugin {
        // Values are way too low for testing, in real usage set them much higher
        private static readonly TimeSpan UpdatePeriod = TimeSpan.FromSeconds(3d);
        private static readonly TimeSpan UpdateRainPeriod = TimeSpan.FromSeconds(0.5d);
        private static readonly TimeSpan TimeBetweenWeatherChangesMin = TimeSpan.FromSeconds(10d);
        private static readonly TimeSpan TimeBetweenWeatherChangesMax = TimeSpan.FromSeconds(20d);

        private class WeatherInfo {
            public double RainIntensity;
        }

        private static readonly Dictionary<CommandWeatherType, WeatherInfo> WeatherInfos = new Dictionary<CommandWeatherType, WeatherInfo> {
            { CommandWeatherType.LightDrizzle, new WeatherInfo { RainIntensity = 0.01 } },
            { CommandWeatherType.Drizzle, new WeatherInfo { RainIntensity = 0.02 } },
            { CommandWeatherType.HeavyDrizzle, new WeatherInfo { RainIntensity = 0.03 } },
            { CommandWeatherType.LightRain, new WeatherInfo { RainIntensity = 0.05 } },
            { CommandWeatherType.Rain, new WeatherInfo { RainIntensity = 0.1 } },
            { CommandWeatherType.HeavyRain, new WeatherInfo { RainIntensity = 0.2 } },
            { CommandWeatherType.LightThunderstorm, new WeatherInfo { RainIntensity = 0.3 } },
            { CommandWeatherType.Thunderstorm, new WeatherInfo { RainIntensity = 0.5 } },
            { CommandWeatherType.HeavyThunderstorm, new WeatherInfo { RainIntensity = 0.6 } },
            { CommandWeatherType.LightSleet, new WeatherInfo { RainIntensity = 0 } },
            { CommandWeatherType.Sleet, new WeatherInfo { RainIntensity = 0.05 } },
            { CommandWeatherType.HeavySleet, new WeatherInfo { RainIntensity = 0.1 } },
            { CommandWeatherType.Clear, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.FewClouds, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.ScatteredClouds, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.BrokenClouds, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.OvercastClouds, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Fog, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Mist, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Cold, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Hot, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Windy, new WeatherInfo { RainIntensity = 0d } },
            { CommandWeatherType.Hail, new WeatherInfo { RainIntensity = 0.2 } }
        };

        private bool _disposed;

        public override void OnConnected() {
            BroadcastLoopAsync().Ignore();
            SimulateRainAsync().Ignore();
        }

        public override void Dispose() {
            _disposed = true;
        }

        public override void OnClientLoaded(MsgClientLoaded msg) {
            // Sending conditions directly to all newly connected:
            PluginManager.SendCspCommand(msg.CarId, GetCspCommand());
        }

        private readonly DateTime _startingDate = DateTime.Now;
        private readonly Stopwatch _timePassedTotal = Stopwatch.StartNew();
        private CommandWeatherType _currentWeather = CommandWeatherType.Clear;
        private CommandWeatherType _nextWeather = WeatherInfos.Keys.RandomElement();
        private readonly Stopwatch _weatherTransitionTimePassed = Stopwatch.StartNew();
        private TimeSpan _weatherTransitionDuration = TimeSpan.Zero;
        private double _currentWindSpeed;
        private double _nextWindSpeed;
        private double _currentWindDirection;
        private double _nextWindDirection;
        private double _currentHumidity;
        private double _nextHumidity;
        private double _currentPressure;
        private double _nextPressure;

        private double _rainIntensity;
        private double _rainWetness;
        private double _rainWater;

        private double CalculateRainIntensity() {
            var transition = (_weatherTransitionTimePassed.Elapsed.TotalSeconds / _weatherTransitionDuration.TotalSeconds).Saturate().SmoothStep();
            return (Half)transition.Lerp(WeatherInfos[_currentWeather].RainIntensity, WeatherInfos[_nextWeather].RainIntensity);
        }

        private CommandWeatherSetV3 GetCspCommand() {
            if (_weatherTransitionTimePassed.Elapsed >= _weatherTransitionDuration) {
                _weatherTransitionDuration = MathUtils.Random(TimeBetweenWeatherChangesMin, TimeBetweenWeatherChangesMax);
                _weatherTransitionTimePassed.Restart();
                _currentWeather = _nextWeather;
                _nextWeather = WeatherInfos.Keys.RandomElement();
                _currentWindSpeed = _nextWindSpeed;
                _nextWindSpeed = MathUtils.Random(20d);
                _currentWindDirection = _nextWindDirection;
                _nextWindDirection = MathUtils.Random(360d);
                _currentHumidity = _nextHumidity;
                _nextHumidity = MathUtils.Random(100d);
                _currentPressure = _nextPressure;
                _nextPressure = MathUtils.Random(950d, 1050d);
            }

            var transition = (_weatherTransitionTimePassed.Elapsed.TotalSeconds / _weatherTransitionDuration.TotalSeconds).Saturate().SmoothStep();
            return new CommandWeatherSetV3 {
                Timestamp = (ulong)(_startingDate + _timePassedTotal.Elapsed).ToUnixTimestamp(),
                TimeToApply = (Half)UpdatePeriod.TotalSeconds,
                WeatherCurrent = _currentWeather,
                WeatherNext = _nextWeather,
                Transition = (ushort)(65535 * transition),
                WindSpeedKmh = (Half)transition.Lerp(_currentWindSpeed, _nextWindSpeed),
                WindDirectionDeg = (Half)transition.Lerp(_currentWindDirection, _nextWindDirection),
                Humidity = (byte)(transition.Lerp(_currentHumidity, _nextHumidity) * 255d),
                Pressure = (Half)transition.Lerp(_currentPressure, _nextPressure),
                TemperatureAmbient = 25,
                TemperatureRoad = 25,
                TrackGrip = 95d,
                RainIntensity = (Half)_rainIntensity,
                RainWetness = (Half)_rainWetness,
                RainWater = (Half)_rainWater
            };
        }

        private async Task BroadcastLoopAsync() {
            while (!_disposed) {
                if (PluginManager.IsConnected) {
                    PluginManager.BroadcastCspCommand(GetCspCommand());
                }
                await Task.Delay(UpdatePeriod);
            }
        }

        private async Task SimulateRainAsync() {
            while (!_disposed) {
                _rainIntensity = CalculateRainIntensity();
                _rainWetness += (_rainIntensity - _rainWetness) * (_rainIntensity > 0 ? _rainIntensity.Lerp(0.02, 1d) : 0.05);
                _rainWater += (Math.Pow(_rainIntensity, 0.1) - _rainWater) * (_rainIntensity > 0 ? _rainIntensity.Lerp(0.02, 1d) : 0.05) * 0.01;
                await Task.Delay(UpdateRainPeriod);
            }
        }
    }
}