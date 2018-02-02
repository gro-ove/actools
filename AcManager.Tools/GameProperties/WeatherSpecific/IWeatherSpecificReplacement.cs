using AcManager.Tools.Objects;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    internal interface IWeatherSpecificReplacement {
        bool Apply(WeatherObject weather);

        bool Revert();
    }
}