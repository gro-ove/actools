using AcManager.Tools.Data;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers {
    public static class WeatherProvider {
        public static WeatherDescription TryToGetWeather(GeoTagsEntry geoTags) {
            return new OpenWeatherApiProvider().TryToGetWeather(geoTags);
        }
    }
}
