using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class WeatherDescription {
        [System.Diagnostics.Contracts.Pure]
        public static WeatherType? FindClosestWeather(IEnumerable<WeatherType> list, WeatherType type) {
            if (type == WeatherType.None) return null;

            var weatherTypes = list.Select(x => (WeatherType?)x).ToList();
            for (var i = 0; i < 5; i++) {
                if (weatherTypes.Contains(type)) return type;

                switch (type) {
                    case WeatherType.LightThunderstorm:
                    case WeatherType.Thunderstorm:
                    case WeatherType.HeavyThunderstorm:
                        return new WeatherType?[] {
                            WeatherType.Thunderstorm,
                            WeatherType.HeavyThunderstorm,
                            WeatherType.LightThunderstorm,

                            WeatherType.HeavyRain,
                            WeatherType.Rain,
                            WeatherType.HeavyDrizzle,
                            WeatherType.Drizzle,
                            WeatherType.LightRain,
                            WeatherType.LightDrizzle,
                            WeatherType.Hurricane,

                            WeatherType.OvercastClouds,
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.LightDrizzle:
                    case WeatherType.HeavyDrizzle:
                    case WeatherType.Drizzle:
                        return new WeatherType?[] {
                            WeatherType.Drizzle,
                            WeatherType.HeavyDrizzle,
                            WeatherType.LightDrizzle,

                            WeatherType.Rain,
                            WeatherType.LightRain,
                            WeatherType.HeavyRain,
                            WeatherType.LightThunderstorm,

                            WeatherType.OvercastClouds,
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.LightRain:
                    case WeatherType.Rain:
                    case WeatherType.HeavyRain:
                        return new WeatherType?[] {
                            WeatherType.Rain,
                            WeatherType.HeavyRain,
                            WeatherType.LightRain,

                            WeatherType.HeavyDrizzle,
                            WeatherType.Drizzle,
                            WeatherType.LightThunderstorm,
                            WeatherType.Thunderstorm,
                            WeatherType.HeavyThunderstorm,
                            WeatherType.LightDrizzle,

                            WeatherType.OvercastClouds,
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.LightSnow:
                    case WeatherType.Snow:
                    case WeatherType.HeavySnow:
                        return new WeatherType?[] {
                            WeatherType.Snow,
                            WeatherType.HeavySnow,
                            WeatherType.LightSnow,

                            WeatherType.Sleet,
                            WeatherType.HeavySleet,
                            WeatherType.LightSleet,

                            WeatherType.OvercastClouds,
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.LightSleet:
                    case WeatherType.Sleet:
                    case WeatherType.HeavySleet:
                        return new WeatherType?[] {
                            WeatherType.Sleet,
                            WeatherType.HeavySleet,
                            WeatherType.LightSleet,

                            WeatherType.Snow,
                            WeatherType.HeavySnow,
                            WeatherType.LightSnow,

                            WeatherType.OvercastClouds,
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.Clear:
                        return new WeatherType?[] {
                            WeatherType.FewClouds,
                            WeatherType.ScatteredClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.FewClouds:
                        return new WeatherType?[] {
                            WeatherType.Clear,
                            WeatherType.ScatteredClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.ScatteredClouds:
                    case WeatherType.BrokenClouds:
                    case WeatherType.OvercastClouds:
                        return new WeatherType?[] {
                            WeatherType.BrokenClouds,
                            WeatherType.ScatteredClouds,
                            WeatherType.OvercastClouds,
                            WeatherType.FewClouds,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.Fog:
                    case WeatherType.Mist:
                    case WeatherType.Smoke:
                    case WeatherType.Haze:
                        return new WeatherType?[] {
                            WeatherType.Fog,
                            WeatherType.Mist,
                            WeatherType.Smoke,
                            WeatherType.Haze,
                            WeatherType.Sand,
                            WeatherType.Dust,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.Sand:
                    case WeatherType.Dust:
                        return new WeatherType?[] {
                            WeatherType.Sand,
                            WeatherType.Dust,
                            WeatherType.Fog,
                            WeatherType.Mist,
                            WeatherType.Smoke,
                            WeatherType.Haze,
                        }.Intersect(weatherTypes).FirstOrDefault();

                    case WeatherType.Squalls:
                    case WeatherType.Tornado:
                    case WeatherType.Hurricane:
                    case WeatherType.Hail:
                        var findClosestWeather = new WeatherType?[] {
                            WeatherType.Tornado,
                            WeatherType.Hurricane,
                        }.Intersect(weatherTypes).FirstOrDefault();
                        if (findClosestWeather != null) return findClosestWeather;
                        type = WeatherType.HeavyThunderstorm;
                        continue;

                    case WeatherType.Cold:
                    case WeatherType.Windy:
                        var firstOrDefault = new WeatherType?[] {
                            WeatherType.Cold,
                            WeatherType.Windy,
                        }.Intersect(weatherTypes).FirstOrDefault();
                        if (firstOrDefault != null) return firstOrDefault;
                        type = WeatherType.ScatteredClouds;
                        continue;

                    case WeatherType.Hot:
                        return WeatherType.Clear;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }

            return null;
        }

        public WeatherType Type { get; }

        public double Temperature { get; }
        
        public double WindSpeed { get; }
        
        public double WindDirection { get; }

        [CanBeNull]
        public string Icon { get; }

        public string Description { get; }

        public string FullDescription => $"{Description.Substring(0, 1).ToUpper() + Description.Substring(1)} ({Temperature:F1} °C)";

        public bool HasIcon => Icon != null;

        public WeatherDescription(WeatherType type, double temperature, string description, double windSpeed, double windDirection, string icon = null) {
            Type = type;
            Temperature = temperature;
            Description = description;
            WindSpeed = windSpeed;
            WindDirection = windDirection;
            Icon = icon;
        }
    }
}
