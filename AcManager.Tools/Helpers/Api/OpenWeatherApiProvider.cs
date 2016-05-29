using System;
using System.Globalization;
using System.Net;
using System.Xml;
using AcManager.Internal;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

// ReSharper disable PossibleNullReferenceException
// try-catch will catch them and return null, relax

namespace AcManager.Tools.Helpers.Api {
    public partial class OpenWeatherApiProvider {
        public enum OpenWeatherType {
            ThunderstormWithLightRain = 200,
            ThunderstormWithRain = 201,
            ThunderstormWithHeavyRain = 202,
            LightThunderstorm = 210,
            Thunderstorm = 211,
            HeavyThunderstorm = 212,
            RaggedThunderstorm = 221,
            ThunderstormWithLightDrizzle = 230,
            ThunderstormWithDrizzle = 231,
            ThunderstormWithHeavyDrizzle = 232,
            LightIntensityDrizzle = 300,
            Drizzle = 301,
            HeavyIntensityDrizzle = 302,
            LightIntensityDrizzleRain = 310,
            DrizzleRain = 311,
            HeavyIntensityDrizzleRain = 312,
            ShowerRainAndDrizzle = 313,
            HeavyShowerRainAndDrizzle = 314,
            ShowerDrizzle = 321,
            LightRain = 500,
            ModerateRain = 501,
            HeavyIntensityRain = 502,
            VeryHeavyRain = 503,
            ExtremeRain = 504,
            FreezingRain = 511,
            LightIntensityShowerRain = 520,
            ShowerRain = 521,
            HeavyIntensityShowerRain = 522,
            RaggedShowerRain = 531,
            LightSnow = 600,
            Snow = 601,
            HeavySnow = 602,
            Sleet = 611,
            ShowerSleet = 612,
            LightRainAndSnow = 615,
            RainAndSnow = 616,
            LightShowerSnow = 620,
            ShowerSnow = 621,
            HeavyShowerSnow = 622,
            Mist = 701,
            Smoke = 711,
            Haze = 721,
            SandAndDustWhirls = 731,
            Fog = 741,
            Sand = 751,
            Dust = 761,
            VolcanicAsh = 762,
            Squalls = 771,
            Tornado = 781,
            ClearSky = 800,
            FewClouds = 801,
            ScatteredClouds = 802,
            BrokenClouds = 803,
            OvercastClouds = 804,
            TornadoExtreme = 900,
            TropicalStorm = 901,
            Hurricane = 902,
            Cold = 903,
            Hot = 904,
            Windy = 905,
            Hail = 906,
            Calm = 951,
            LightBreeze = 952,
            GentleBreeze = 953,
            ModerateBreeze = 954,
            FreshBreeze = 955,
            StrongBreeze = 956,
            HighWind, NearGale = 957,
            Gale = 958,
            SevereGale = 959,
            Storm = 960,
            ViolentStorm = 961,
            HurricaneAdditional = 962,
        }

        private static WeatherDescription.WeatherType OpenWeatherTypeToCommonType(OpenWeatherType type) {
            switch (type) {
                case OpenWeatherType.RaggedThunderstorm:
                case OpenWeatherType.Thunderstorm:
                case OpenWeatherType.ThunderstormWithLightRain:
                case OpenWeatherType.ThunderstormWithRain:
                case OpenWeatherType.ThunderstormWithHeavyRain:
                case OpenWeatherType.ThunderstormWithLightDrizzle:
                case OpenWeatherType.ThunderstormWithDrizzle:
                case OpenWeatherType.ThunderstormWithHeavyDrizzle:
                    return WeatherDescription.WeatherType.Thunderstorm;

                case OpenWeatherType.LightThunderstorm:
                    return WeatherDescription.WeatherType.LightThunderstorm;

                case OpenWeatherType.HeavyThunderstorm:
                case OpenWeatherType.TropicalStorm:
                    return WeatherDescription.WeatherType.HeavyThunderstorm;

                case OpenWeatherType.LightIntensityDrizzle:
                case OpenWeatherType.LightIntensityDrizzleRain:
                    return WeatherDescription.WeatherType.LightDrizzle;

                case OpenWeatherType.Drizzle:
                case OpenWeatherType.DrizzleRain:
                case OpenWeatherType.ShowerDrizzle:
                    return WeatherDescription.WeatherType.Drizzle;

                case OpenWeatherType.HeavyIntensityDrizzle:
                case OpenWeatherType.HeavyIntensityDrizzleRain:
                    return WeatherDescription.WeatherType.HeavyDrizzle;

                case OpenWeatherType.LightRain:
                case OpenWeatherType.LightIntensityShowerRain:
                    return WeatherDescription.WeatherType.LightRain;

                case OpenWeatherType.ModerateRain:
                case OpenWeatherType.FreezingRain:
                case OpenWeatherType.ShowerRainAndDrizzle:
                case OpenWeatherType.ShowerRain:
                case OpenWeatherType.RaggedShowerRain:
                    return WeatherDescription.WeatherType.Rain;

                case OpenWeatherType.HeavyIntensityRain:
                case OpenWeatherType.VeryHeavyRain:
                case OpenWeatherType.ExtremeRain:
                case OpenWeatherType.HeavyShowerRainAndDrizzle:
                case OpenWeatherType.HeavyIntensityShowerRain:
                    return WeatherDescription.WeatherType.HeavyRain;
                    
                case OpenWeatherType.LightSnow:
                case OpenWeatherType.LightShowerSnow:
                    return WeatherDescription.WeatherType.LightSnow;

                case OpenWeatherType.Snow:
                case OpenWeatherType.ShowerSnow:
                    return WeatherDescription.WeatherType.Snow;

                case OpenWeatherType.HeavySnow:
                case OpenWeatherType.HeavyShowerSnow:
                    return WeatherDescription.WeatherType.HeavySnow;
                    
                case OpenWeatherType.LightRainAndSnow:
                    return WeatherDescription.WeatherType.LightSleet;

                case OpenWeatherType.RainAndSnow:
                case OpenWeatherType.Sleet:
                    return WeatherDescription.WeatherType.Sleet;
                    
                case OpenWeatherType.ShowerSleet:
                    return WeatherDescription.WeatherType.HeavySleet;

                case OpenWeatherType.Mist:
                    return WeatherDescription.WeatherType.Mist;

                case OpenWeatherType.Smoke:
                    return WeatherDescription.WeatherType.Smoke;

                case OpenWeatherType.Haze:
                    return WeatherDescription.WeatherType.Haze;
                    
                case OpenWeatherType.Sand:
                case OpenWeatherType.SandAndDustWhirls:
                    return WeatherDescription.WeatherType.Sand;
                    
                case OpenWeatherType.Dust:
                case OpenWeatherType.VolcanicAsh:
                    return WeatherDescription.WeatherType.Dust;

                case OpenWeatherType.Fog:
                    return WeatherDescription.WeatherType.Fog;

                case OpenWeatherType.Squalls:
                    return WeatherDescription.WeatherType.Squalls;

                case OpenWeatherType.Tornado:
                case OpenWeatherType.TornadoExtreme:
                    return WeatherDescription.WeatherType.Tornado;

                case OpenWeatherType.ClearSky:
                case OpenWeatherType.Calm:
                case OpenWeatherType.LightBreeze:
                    return WeatherDescription.WeatherType.Clear;

                case OpenWeatherType.FewClouds:
                case OpenWeatherType.GentleBreeze:
                case OpenWeatherType.ModerateBreeze:
                    return WeatherDescription.WeatherType.FewClouds;

                case OpenWeatherType.ScatteredClouds:
                    return WeatherDescription.WeatherType.ScatteredClouds;

                case OpenWeatherType.BrokenClouds:
                    return WeatherDescription.WeatherType.BrokenClouds;

                case OpenWeatherType.OvercastClouds:
                    return WeatherDescription.WeatherType.OvercastClouds;

                case OpenWeatherType.Hurricane:
                case OpenWeatherType.Gale:
                case OpenWeatherType.SevereGale:
                case OpenWeatherType.Storm:
                case OpenWeatherType.ViolentStorm:
                case OpenWeatherType.HurricaneAdditional:
                    return WeatherDescription.WeatherType.Hurricane;

                case OpenWeatherType.Cold:
                    return WeatherDescription.WeatherType.Cold;

                case OpenWeatherType.Hot:
                    return WeatherDescription.WeatherType.Hot;

                case OpenWeatherType.Windy:
                case OpenWeatherType.FreshBreeze:
                case OpenWeatherType.StrongBreeze:
                case OpenWeatherType.HighWind:
                    return WeatherDescription.WeatherType.Windy;

                case OpenWeatherType.Hail:
                    return WeatherDescription.WeatherType.Hail;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private const string RequestWeatherUri = "http://api.openweathermap.org/data/2.5/weather?lat={0}&lon={1}&APPID={2}&mode=xml&units=metric";
        private const string IconUri = "http://openweathermap.org/img/w/{0}.png";

        public WeatherDescription TryToGetWeather(GeoTagsEntry geoTags) {
            var requestUri = string.Format(RequestWeatherUri, geoTags.LatitudeValue, geoTags.LongitudeValue, InternalUtils.GetOpenWeatherApiCode());

            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                using (var response = (HttpWebResponse) httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    using (var responseStream = response.GetResponseStream()) {
                        if (responseStream == null) return null;

                        var xml = new XmlDocument();
                        xml.Load(responseStream);

                        var temperatureNode = xml.GetElementsByTagName("temperature")[0];
                        var weatherNode = xml.GetElementsByTagName("weather")[0];

                        var temperature = double.Parse(temperatureNode.Attributes["value"].Value, CultureInfo.InvariantCulture);
                        var type = OpenWeatherTypeToCommonType((OpenWeatherType)int.Parse(weatherNode.Attributes["number"].Value, NumberStyles.Any, CultureInfo.InvariantCulture));
                        var description = weatherNode.Attributes["value"].Value;
                        var iconAttribute = weatherNode.Attributes["icon"];
                        var iconUri = iconAttribute == null ? null : string.Format(IconUri, iconAttribute.Value);
                        return new WeatherDescription(type, temperature, description, iconUri);
                    }
                }
            } catch (Exception e) {
                Logging.Warning("cannot get weather using openweather: {0}\n{1}", requestUri, e);
                return null;
            }
        }
    }
}
