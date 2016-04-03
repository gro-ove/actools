using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;

namespace AcManager.Tools.Data {
    public class WeatherDescription {
        public enum WeatherType {
            LightThunderstorm,
            Thunderstorm,
            HeavyThunderstorm,

            LightDrizzle,
            Drizzle,
            HeavyDrizzle,

            LightRain,
            Rain,
            HeavyRain,

            LightSnow,
            Snow,
            HeavySnow,

            LightSleet,
            Sleet,
            HeavySleet,

            Clear,
            FewClouds,
            ScatteredClouds,
            BrokenClouds,
            OvercastClouds,

            Fog,
            Mist, 
            
            Smoke, Haze, Sand, Dust, Squalls,
            Tornado, Hurricane, Cold, Hot, Windy, Hail
        }

        public static string GetWeatherTypeName (WeatherType type) {
            return Regex.Replace(type.ToString(), @"(?=[A-Z])", " ").TrimStart();
        }
        
        [Pure]
        public static WeatherType? FindClosestWeather(IEnumerable<WeatherType> list, WeatherType type) {
            var weatherTypes = list.ToList();
            for (var i = 0; i < 5; i++) {
                if (weatherTypes.Contains(type)) return type;

                switch (type) {
                    case WeatherType.LightThunderstorm:
                    case WeatherType.Thunderstorm:
                    case WeatherType.HeavyThunderstorm:
                        return new[] {
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
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.LightDrizzle:
                    case WeatherType.HeavyDrizzle:
                    case WeatherType.Drizzle:
                        return new[] {
                            WeatherType.Drizzle,
                            WeatherType.HeavyDrizzle, 
                            WeatherType.LightDrizzle,
 
                            WeatherType.Rain, 
                            WeatherType.LightRain, 
                            WeatherType.HeavyRain, 
                            WeatherType.LightThunderstorm, 

                            WeatherType.OvercastClouds, 
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.LightRain:
                    case WeatherType.Rain:
                    case WeatherType.HeavyRain:
                        return new[] {
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
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.LightSnow:
                    case WeatherType.Snow:
                    case WeatherType.HeavySnow:
                        return new[] {
                            WeatherType.Snow, 
                            WeatherType.HeavySnow,
                            WeatherType.LightSnow, 

                            WeatherType.Sleet, 
                            WeatherType.HeavySleet, 
                            WeatherType.LightSleet, 
                            
                            WeatherType.OvercastClouds, 
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.LightSleet:
                    case WeatherType.Sleet:
                    case WeatherType.HeavySleet:
                        return new[] {
                            WeatherType.Sleet, 
                            WeatherType.HeavySleet, 
                            WeatherType.LightSleet, 
                            
                            WeatherType.Snow, 
                            WeatherType.HeavySnow, 
                            WeatherType.LightSnow, 
                            
                            WeatherType.OvercastClouds, 
                            WeatherType.BrokenClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.Clear:
                        return new[] {
                            WeatherType.FewClouds, 
                            WeatherType.ScatteredClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.FewClouds:
                        return new[] {
                            WeatherType.Clear, 
                            WeatherType.ScatteredClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.ScatteredClouds:
                    case WeatherType.BrokenClouds:
                    case WeatherType.OvercastClouds:
                        return new[] {
                            WeatherType.BrokenClouds, 
                            WeatherType.ScatteredClouds, 
                            WeatherType.OvercastClouds, 
                            WeatherType.FewClouds,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.Fog:
                    case WeatherType.Mist:
                    case WeatherType.Smoke:
                    case WeatherType.Haze:
                        return new[] {
                            WeatherType.Fog, 
                            WeatherType.Mist, 
                            WeatherType.Smoke, 
                            WeatherType.Haze, 
                            WeatherType.Sand, 
                            WeatherType.Dust,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.Sand:
                    case WeatherType.Dust:
                        return new[] {
                            WeatherType.Sand, 
                            WeatherType.Dust, 
                            WeatherType.Fog,
                            WeatherType.Mist, 
                            WeatherType.Smoke, 
                            WeatherType.Haze,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();

                    case WeatherType.Squalls:
                    case WeatherType.Tornado:
                    case WeatherType.Hurricane:
                    case WeatherType.Hail:
                        var findClosestWeather = new[] {
                            WeatherType.Tornado, 
                            WeatherType.Hurricane,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();
                        if (findClosestWeather != null) return findClosestWeather;
                        type = WeatherType.HeavyThunderstorm;
                        continue;

                    case WeatherType.Cold:
                    case WeatherType.Windy:
                        var firstOrDefault = new[] {
                            WeatherType.Cold, 
                            WeatherType.Windy,
                        }.Intersect(weatherTypes).Cast<WeatherType?>().FirstOrDefault();
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

        public string Icon { get; }

        public string Description { get; }

        public string FullDescription => $@"{Description.Substring(0, 1).ToUpper() + Description.Substring(1)} ({Temperature:F1}°C)";

        public bool HasIcon => Icon != null;

        public WeatherDescription(WeatherType type, double temperature, string description, string icon = null) {
            Type = type;
            Temperature = temperature;
            Description = description;
            Icon = icon;
        }
    }
}
