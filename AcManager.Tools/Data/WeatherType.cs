using System.ComponentModel;

namespace AcManager.Tools.Data {
    public enum WeatherType {
        [Description("Light thunderstorm")]
        LightThunderstorm,
        [Description("Thunderstorm")]
        Thunderstorm,
        [Description("Heavy thunderstorm")]
        HeavyThunderstorm,

        [Description("Light drizzle")]
        LightDrizzle,
        [Description("Drizzle")]
        Drizzle,
        [Description("Heavy drizzle")]
        HeavyDrizzle,

        [Description("Light rain")]
        LightRain,
        [Description("Rain")]
        Rain,
        [Description("Heavy rain")]
        HeavyRain,

        [Description("Light snow")]
        LightSnow,
        [Description("Snow")]
        Snow,
        [Description("Heavy snow")]
        HeavySnow,

        [Description("Light sleet")]
        LightSleet,
        [Description("Sleet")]
        Sleet,
        [Description("Heavy sleet")]
        HeavySleet,

        [Description("Clear")]
        Clear,
        [Description("Few clouds")]
        FewClouds,
        [Description("Scattered clouds")]
        ScatteredClouds,
        [Description("Broken clouds")]
        BrokenClouds,
        [Description("Overcast clouds")]
        OvercastClouds,

        [Description("Fog")]
        Fog,
        [Description("Mist")]
        Mist,

        [Description("Smoke")]
        Smoke,
        [Description("Haze")]
        Haze,
        [Description("Sand")]
        Sand,
        [Description("Dust")]
        Dust,
        [Description("Squalls")]
        Squalls,

        [Description("Tornado")]
        Tornado,
        [Description("Hurricane")]
        Hurricane,
        [Description("Cold")]
        Cold,
        [Description("Hot")]
        Hot,
        [Description("Windy")]
        Windy,
        [Description("Hail")]
        Hail
    }
}