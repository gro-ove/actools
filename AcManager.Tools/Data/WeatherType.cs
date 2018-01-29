using System;
using System.Collections.Generic;

namespace AcManager.Tools.Data {
    public class WeatherFallbackAttribute : Attribute {
        private Dictionary<WeatherType, double> _distances = new Dictionary<WeatherType, double>();
        public IReadOnlyDictionary<WeatherType, double> Distances => _distances;

        public double this[WeatherType key] {
            get => _distances[key];
            set => _distances[key] = value;
        }

        public WeatherFallbackAttribute(WeatherType fallbackTo, double distance = 1) {
            this[fallbackTo] = distance;
        }

        public WeatherFallbackAttribute(params WeatherType[] fallbackTo) {
            foreach (var x in fallbackTo) {
                this[x] = 1;
            }
        }
    }

    public enum WeatherType {
        [LocalizedDescription("Common_None")]
        None = -1,

        [LocalizedDescription("Weather_LightThunderstorm"), WeatherFallback(Thunderstorm)]
        LightThunderstorm = 0,

        [LocalizedDescription("Weather_Thunderstorm"), WeatherFallback(HeavyRain)]
        Thunderstorm = 1,

        [LocalizedDescription("Weather_HeavyThunderstorm"), WeatherFallback(Thunderstorm)]
        HeavyThunderstorm = 2,

        [LocalizedDescription("Weather_LightDrizzle"), WeatherFallback(Drizzle)]
        LightDrizzle = 3,

        [LocalizedDescription("Weather_Drizzle"), WeatherFallback(LightRain)]
        Drizzle = 4,

        [LocalizedDescription("Weather_HeavyDrizzle"), WeatherFallback(Drizzle, Rain)]
        HeavyDrizzle = 5,

        [LocalizedDescription("Weather_LightRain"), WeatherFallback(Rain)]
        LightRain = 6,

        [LocalizedDescription("Weather_Rain"), WeatherFallback(Fog, 10)]
        Rain = 7,

        [LocalizedDescription("Weather_HeavyRain"), WeatherFallback(Rain)]
        HeavyRain = 8,

        [LocalizedDescription("Weather_LightSnow"), WeatherFallback(Snow)]
        LightSnow = 9,

        [LocalizedDescription("Weather_Snow"), WeatherFallback(Fog, 10)]
        Snow = 10,

        [LocalizedDescription("Weather_HeavySnow"), WeatherFallback(Snow)]
        HeavySnow = 11,

        [LocalizedDescription("Weather_LightSleet"), WeatherFallback(Sleet, LightSnow)]
        LightSleet = 12,

        [LocalizedDescription("Weather_Sleet"), WeatherFallback(Snow)]
        Sleet = 13,

        [LocalizedDescription("Weather_HeavySleet"), WeatherFallback(Sleet, HeavySnow)]
        HeavySleet = 14,

        [LocalizedDescription("Weather_Clear"), WeatherFallback(FewClouds)]
        Clear = 15,

        [LocalizedDescription("Weather_FewClouds"), WeatherFallback(ScatteredClouds)]
        FewClouds = 16,

        [LocalizedDescription("Weather_ScatteredClouds"), WeatherFallback(BrokenClouds)]
        ScatteredClouds = 17,

        [LocalizedDescription("Weather_BrokenClouds"), WeatherFallback(OvercastClouds)]
        BrokenClouds = 18,

        [LocalizedDescription("Weather_OvercastClouds"), WeatherFallback(Fog, 10)]
        OvercastClouds = 19,

        [LocalizedDescription("Weather_Fog")]
        Fog = 20,

        [LocalizedDescription("Weather_Mist"), WeatherFallback(Fog)]
        Mist = 21,

        [LocalizedDescription("Weather_Smoke"), WeatherFallback(Mist)]
        Smoke = 22,

        [LocalizedDescription("Weather_Haze"), WeatherFallback(Smoke)]
        Haze = 23,

        [LocalizedDescription("Weather_Sand"), WeatherFallback(Smoke)]
        Sand = 24,

        [LocalizedDescription("Weather_Dust"), WeatherFallback(Sand)]
        Dust = 25,

        [LocalizedDescription("Weather_Squalls"), WeatherFallback(HeavyThunderstorm)]
        Squalls = 26,

        [LocalizedDescription("Weather_Tornado"), WeatherFallback(HeavyThunderstorm)]
        Tornado = 27,

        [LocalizedDescription("Weather_Hurricane"), WeatherFallback(HeavyThunderstorm)]
        Hurricane = 28,

        [LocalizedDescription("Weather_Cold"), WeatherFallback(Clear)]
        Cold = 29,

        [LocalizedDescription("Weather_Hot"), WeatherFallback(Clear)]
        Hot = 30,

        [LocalizedDescription("Weather_Windy"), WeatherFallback(BrokenClouds)]
        Windy = 31,

        [LocalizedDescription("Weather_Hail"), WeatherFallback(HeavySnow)]
        Hail = 32
    }
}