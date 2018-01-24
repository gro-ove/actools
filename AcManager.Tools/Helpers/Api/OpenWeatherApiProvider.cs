using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AcManager.Internal;
using AcManager.Tools.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.Api {
    public class OpenWeatherApiProvider {
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

        private static WeatherType OpenWeatherTypeToCommonType(OpenWeatherType type) {
            switch (type) {
                case OpenWeatherType.RaggedThunderstorm:
                case OpenWeatherType.Thunderstorm:
                case OpenWeatherType.ThunderstormWithLightRain:
                case OpenWeatherType.ThunderstormWithRain:
                case OpenWeatherType.ThunderstormWithHeavyRain:
                case OpenWeatherType.ThunderstormWithLightDrizzle:
                case OpenWeatherType.ThunderstormWithDrizzle:
                case OpenWeatherType.ThunderstormWithHeavyDrizzle:
                    return WeatherType.Thunderstorm;

                case OpenWeatherType.LightThunderstorm:
                    return WeatherType.LightThunderstorm;

                case OpenWeatherType.HeavyThunderstorm:
                case OpenWeatherType.TropicalStorm:
                    return WeatherType.HeavyThunderstorm;

                case OpenWeatherType.LightIntensityDrizzle:
                case OpenWeatherType.LightIntensityDrizzleRain:
                    return WeatherType.LightDrizzle;

                case OpenWeatherType.Drizzle:
                case OpenWeatherType.DrizzleRain:
                case OpenWeatherType.ShowerDrizzle:
                    return WeatherType.Drizzle;

                case OpenWeatherType.HeavyIntensityDrizzle:
                case OpenWeatherType.HeavyIntensityDrizzleRain:
                    return WeatherType.HeavyDrizzle;

                case OpenWeatherType.LightRain:
                case OpenWeatherType.LightIntensityShowerRain:
                    return WeatherType.LightRain;

                case OpenWeatherType.ModerateRain:
                case OpenWeatherType.FreezingRain:
                case OpenWeatherType.ShowerRainAndDrizzle:
                case OpenWeatherType.ShowerRain:
                case OpenWeatherType.RaggedShowerRain:
                    return WeatherType.Rain;

                case OpenWeatherType.HeavyIntensityRain:
                case OpenWeatherType.VeryHeavyRain:
                case OpenWeatherType.ExtremeRain:
                case OpenWeatherType.HeavyShowerRainAndDrizzle:
                case OpenWeatherType.HeavyIntensityShowerRain:
                    return WeatherType.HeavyRain;

                case OpenWeatherType.LightSnow:
                case OpenWeatherType.LightShowerSnow:
                    return WeatherType.LightSnow;

                case OpenWeatherType.Snow:
                case OpenWeatherType.ShowerSnow:
                    return WeatherType.Snow;

                case OpenWeatherType.HeavySnow:
                case OpenWeatherType.HeavyShowerSnow:
                    return WeatherType.HeavySnow;

                case OpenWeatherType.LightRainAndSnow:
                    return WeatherType.LightSleet;

                case OpenWeatherType.RainAndSnow:
                case OpenWeatherType.Sleet:
                    return WeatherType.Sleet;

                case OpenWeatherType.ShowerSleet:
                    return WeatherType.HeavySleet;

                case OpenWeatherType.Mist:
                    return WeatherType.Mist;

                case OpenWeatherType.Smoke:
                    return WeatherType.Smoke;

                case OpenWeatherType.Haze:
                    return WeatherType.Haze;

                case OpenWeatherType.Sand:
                case OpenWeatherType.SandAndDustWhirls:
                    return WeatherType.Sand;

                case OpenWeatherType.Dust:
                case OpenWeatherType.VolcanicAsh:
                    return WeatherType.Dust;

                case OpenWeatherType.Fog:
                    return WeatherType.Fog;

                case OpenWeatherType.Squalls:
                    return WeatherType.Squalls;

                case OpenWeatherType.Tornado:
                case OpenWeatherType.TornadoExtreme:
                    return WeatherType.Tornado;

                case OpenWeatherType.ClearSky:
                case OpenWeatherType.Calm:
                case OpenWeatherType.LightBreeze:
                    return WeatherType.Clear;

                case OpenWeatherType.FewClouds:
                case OpenWeatherType.GentleBreeze:
                case OpenWeatherType.ModerateBreeze:
                    return WeatherType.FewClouds;

                case OpenWeatherType.ScatteredClouds:
                    return WeatherType.ScatteredClouds;

                case OpenWeatherType.BrokenClouds:
                    return WeatherType.BrokenClouds;

                case OpenWeatherType.OvercastClouds:
                    return WeatherType.OvercastClouds;

                case OpenWeatherType.Hurricane:
                case OpenWeatherType.Gale:
                case OpenWeatherType.SevereGale:
                case OpenWeatherType.Storm:
                case OpenWeatherType.ViolentStorm:
                case OpenWeatherType.HurricaneAdditional:
                    return WeatherType.Hurricane;

                case OpenWeatherType.Cold:
                    return WeatherType.Cold;

                case OpenWeatherType.Hot:
                    return WeatherType.Hot;

                case OpenWeatherType.Windy:
                case OpenWeatherType.FreshBreeze:
                case OpenWeatherType.StrongBreeze:
                case OpenWeatherType.HighWind:
                    return WeatherType.Windy;

                case OpenWeatherType.Hail:
                    return WeatherType.Hail;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private const string RequestWeatherUri = "http://api.openweathermap.org/data/2.5/weather?lat={0}&lon={1}&APPID={2}&mode=xml&units=metric";
        private const string IconUri = "http://openweathermap.org/img/w/{0}.png";

        [ItemNotNull]
        public async Task<WeatherDescription> GetWeatherAsync(GeoTagsEntry geoTags) {
            var requestUri = string.Format(RequestWeatherUri, geoTags.LatitudeValue, geoTags.LongitudeValue, InternalUtils.GetOpenWeatherApiCode());

#if DEBUG
            Logging.Write(requestUri);
#endif

            using (var order = KillerOrder.Create(new WebClient(), 5000)) {
                var data = await order.Victim.DownloadStringTaskAsync(requestUri);

                XDocument doc;
                try {
                    doc = XDocument.Parse(data);
                } catch (XmlException) {
                    Logging.Warning("response: " + data);
                    throw;
                }

                var temperatureValue = doc.Descendants(@"temperature").FirstOrDefault()?.Attribute(@"value")?.Value;
                var weatherNode = doc.Descendants(@"weather").FirstOrDefault();
                var windNode = doc.Descendants(@"wind").FirstOrDefault();
                if (temperatureValue == null || weatherNode == null || windNode == null) throw new Exception("Invalid response");

                var type = OpenWeatherTypeToCommonType((OpenWeatherType)FlexibleParser.ParseInt(weatherNode.Attribute(@"number")?.Value));
                var temperature = FlexibleParser.ParseDouble(temperatureValue);
                var description = weatherNode.Attribute(@"value")?.Value;

                var windSpeed = FlexibleParser.ParseDouble(windNode.Descendants(@"speed").First().Attribute("value")?.Value);
                var windDirection = FlexibleParser.ParseDouble(windNode.Descendants(@"direction").First().Attribute("value")?.Value);

                var humidity = FlexibleParser.ParseDouble(doc.Descendants(@"humidity").FirstOrDefault()?.Attribute(@"value")?.Value);
                var pressure = FlexibleParser.ParseDouble(doc.Descendants(@"pressure").FirstOrDefault()?.Attribute(@"value")?.Value);

                var icon = weatherNode.Attribute(@"icon")?.Value;
                var iconUri = icon == null ? null : string.Format(IconUri, icon);
                return new WeatherDescription(type, temperature, description, windSpeed, windDirection, humidity, pressure, iconUri);
            }
        }
    }
}
