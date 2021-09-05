using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AcManager.Tools.AcPlugins;
using AcManager.Tools.AcPlugins.CspCommands;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.ServerPlugins {
    // Simplest dynamic conditions plugin, uses v1 weather description: only weather type and time of day
    public class DynamicConditionsV1ServerPlugin : AcServerPlugin {
        // Values are way too low for testing, in real usage set them much higher
        private static readonly TimeSpan UpdatePeriod = TimeSpan.FromSeconds(2d);
        private static readonly TimeSpan TimeBetweenWeatherChangesMin = TimeSpan.FromSeconds(10d);
        private static readonly TimeSpan TimeBetweenWeatherChangesMax = TimeSpan.FromSeconds(20d);

        private static readonly CommandWeatherType[] AllowedWeatherTypes = {
            CommandWeatherType.LightDrizzle,
            CommandWeatherType.Drizzle,
            CommandWeatherType.HeavyDrizzle,
            CommandWeatherType.LightRain,
            CommandWeatherType.Rain,
            CommandWeatherType.HeavyRain,
            CommandWeatherType.LightSleet,
            CommandWeatherType.Sleet,
            CommandWeatherType.HeavySleet,
            CommandWeatherType.Clear,
            CommandWeatherType.FewClouds,
            CommandWeatherType.ScatteredClouds,
            CommandWeatherType.BrokenClouds,
            CommandWeatherType.OvercastClouds,
            CommandWeatherType.Fog,
            CommandWeatherType.Mist,
            CommandWeatherType.Cold,
            CommandWeatherType.Hot,
            CommandWeatherType.Windy,
            CommandWeatherType.Hail
        };

        private bool _disposed;

        public override void OnConnected() {
            BroadcastLoopAsync().Ignore();
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
        private CommandWeatherType _nextWeather = AllowedWeatherTypes.RandomElement();
        private readonly Stopwatch _weatherTransitionTimePassed = Stopwatch.StartNew();
        private TimeSpan _weatherTransitionDuration = TimeSpan.Zero;

        private CommandWeatherSetV1 GetCspCommand() {
            if (_weatherTransitionTimePassed.Elapsed >= _weatherTransitionDuration) {
                _weatherTransitionDuration = MathUtils.Random(TimeBetweenWeatherChangesMin, TimeBetweenWeatherChangesMax);
                _weatherTransitionTimePassed.Restart();
                _currentWeather = _nextWeather;
                _nextWeather = AllowedWeatherTypes.RandomElement();
            }

            return new CommandWeatherSetV1 {
                Timestamp = (ulong)(_startingDate + _timePassedTotal.Elapsed).ToUnixTimestamp(),
                TimeToApply = (float)UpdatePeriod.TotalSeconds,
                WeatherCurrent = _currentWeather,
                WeatherNext = _nextWeather,
                Transition = (float)(_weatherTransitionTimePassed.Elapsed.TotalSeconds / _weatherTransitionDuration.TotalSeconds).SmoothStep()
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
    }
}