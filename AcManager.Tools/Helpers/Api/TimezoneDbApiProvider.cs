using System;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Data.TzConvert;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api {
    public class TimezoneDbApiProvider {
        // private const string RequestTimeZoneUri = "https://maps.googleapis.com/maps/api/timezone/xml?location={0},{1}&timestamp={2}&key={3}";
        private const string RequestTimeZoneUri = "https://api.timezonedb.com/v2.1/get-time-zone?key={2}&format=json&by=position&lat={0}&lng={1}";

        [ItemNotNull]
        public static async Task<TimeZoneInfo> DetermineTimeZoneAsync(GeoTagsEntry geoTags) {
            var requestUri = string.Format(RequestTimeZoneUri, geoTags.LatitudeValue, geoTags.LongitudeValue,
                                           InternalUtils.GetTimeZoneDbApiCode());
            #if DEBUG
            Logging.Debug(requestUri);
            #endif

            using (var order = KillerOrder.Create(new CookieAwareWebClient(), 15000)) {
                var data = await order.Victim.DownloadStringTaskAsync(requestUri);

                var doc = JObject.Parse(data);
                var zoneId = doc.GetStringValueOnly("zoneName");
                if (zoneId == null) throw new Exception("Invalid response");

                Logging.Write("Returned zone ID: " + zoneId);

                try {
                    Logging.Write("Parsed as: " + TZConvert.GetTimeZoneInfo(zoneId).ToSerializedString());
                    return TZConvert.GetTimeZoneInfo(zoneId);
                } catch (TimeZoneNotFoundException) {
                    var rawOffset = doc.GetStringValueOnly(@"gmtOffset");
                    return TimeZoneInfo.CreateCustomTimeZone(zoneId, TimeSpan.FromSeconds(FlexibleParser.ParseDouble(rawOffset)), zoneId, zoneId);
                }
            }

            /*var requestUri = string.Format(RequestTimeZoneUri, geoTags.LatitudeValue, geoTags.LongitudeValue,
                                           DateTime.Now.ToUnixTimestamp(), InternalUtils.GetGoogleMapsApiCode());

            using (var order = KillerOrder.Create(new WebClient(), 5000)) {
                var data = await order.Victim.DownloadStringTaskAsync(requestUri);
                var doc = XDocument.Parse(data);
                var zoneId = doc.Descendants(@"time_zone_id").FirstOrDefault()?.Value;
                if (zoneId == null) throw new Exception("Invalid response");

                try {
                    return TZConvert.GetTimeZoneInfo(zoneId);
                } catch (TimeZoneNotFoundException) {
                    var rawOffset = doc.Descendants(@"raw_offset").FirstOrDefault()?.Value;
                    var zoneName = doc.Descendants(@"time_zone_name").FirstOrDefault()?.Value;
                    if (rawOffset == null || zoneName == null) throw new Exception("Invalid response");
                    return TimeZoneInfo.CreateCustomTimeZone(zoneId, TimeSpan.FromSeconds(FlexibleParser.ParseDouble(rawOffset)), zoneName, zoneName);
                }
            }*/
        }
    }
}
