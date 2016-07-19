namespace AcManager.Tools.Data {
    public enum WeatherType {
        [LocalizedDescription("Common_None")]
        None = -1,

        [LocalizedDescription("Weather_LightThunderstorm")]
        LightThunderstorm = 0,
        [LocalizedDescription("Weather_Thunderstorm")]
        Thunderstorm = 1,
        [LocalizedDescription("Weather_HeavyThunderstorm")]
        HeavyThunderstorm = 2,

        [LocalizedDescription("Weather_LightDrizzle")]
        LightDrizzle = 3,
        [LocalizedDescription("Weather_Drizzle")]
        Drizzle = 4,
        [LocalizedDescription("Weather_HeavyDrizzle")]
        HeavyDrizzle = 5,

        [LocalizedDescription("Weather_LightRain")]
        LightRain = 6,
        [LocalizedDescription("Weather_Rain")]
        Rain = 7,
        [LocalizedDescription("Weather_HeavyRain")]
        HeavyRain = 8,

        [LocalizedDescription("Weather_LightSnow")]
        LightSnow = 9,
        [LocalizedDescription("Weather_Snow")]
        Snow = 10,
        [LocalizedDescription("Weather_HeavySnow")]
        HeavySnow = 11,

        [LocalizedDescription("Weather_LightSleet")]
        LightSleet = 12,
        [LocalizedDescription("Weather_Sleet")]
        Sleet = 13,
        [LocalizedDescription("Weather_HeavySleet")]
        HeavySleet = 14,

        [LocalizedDescription("Weather_Clear")]
        Clear = 15,
        [LocalizedDescription("Weather_FewClouds")]
        FewClouds = 16,
        [LocalizedDescription("Weather_ScatteredClouds")]
        ScatteredClouds = 17,
        [LocalizedDescription("Weather_BrokenClouds")]
        BrokenClouds = 18,
        [LocalizedDescription("Weather_OvercastClouds")]
        OvercastClouds = 19,

        [LocalizedDescription("Weather_Fog")]
        Fog = 20,
        [LocalizedDescription("Weather_Mist")]
        Mist = 21,

        [LocalizedDescription("Weather_Smoke")]
        Smoke = 22,
        [LocalizedDescription("Weather_Haze")]
        Haze = 23,
        [LocalizedDescription("Weather_Sand")]
        Sand = 24,
        [LocalizedDescription("Weather_Dust")]
        Dust = 25,
        [LocalizedDescription("Weather_Squalls")]
        Squalls = 26,

        [LocalizedDescription("Weather_Tornado")]
        Tornado = 27,
        [LocalizedDescription("Weather_Hurricane")]
        Hurricane = 28,
        [LocalizedDescription("Weather_Cold")]
        Cold = 29,
        [LocalizedDescription("Weather_Hot")]
        Hot = 30,
        [LocalizedDescription("Weather_Windy")]
        Windy = 31,
        [LocalizedDescription("Weather_Hail")]
        Hail = 32
    }
}