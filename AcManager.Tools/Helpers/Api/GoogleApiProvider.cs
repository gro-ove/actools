using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.Api {
    public class GoogleApiProvider {
        private const string RequestTimeZoneUri = "https://maps.googleapis.com/maps/api/timezone/xml?location={0},{1}&timestamp={2}";

        public static TimeZoneInfo TryToDetermineTimeZone(GeoTagsEntry geoTags) {
            var requestUri = string.Format(RequestTimeZoneUri, geoTags.LatitudeValue, geoTags.LongitudeValue,
                                           DateTime.Now.ToUnixTimestamp());
            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";

                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

                using (var response = (HttpWebResponse) httpRequest.GetResponse()) {
                    if (response.StatusCode != HttpStatusCode.OK) return null;

                    using (var responseStream = response.GetResponseStream()) {
                        if (responseStream == null) return null;

                        var xml = new XmlDocument();
                        xml.Load(responseStream);
                        
                        var zoneId = xml.GetElementsByTagName("time_zone_id")[0].InnerText;
                        var rawOffset = xml.GetElementsByTagName("raw_offset")[0].InnerText;

                        try {
                            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
                        } catch (TimeZoneNotFoundException) {
                            var zoneName = xml.GetElementsByTagName("time_zone_name")[0].InnerText;
                            return TimeZoneInfo.CreateCustomTimeZone(zoneId, TimeSpan.FromSeconds(double.Parse(rawOffset, NumberStyles.Any, CultureInfo.InvariantCulture)), zoneName, zoneName);
                        }
                    }
                }
            } catch (Exception e) {
                Logging.Warning($"Cannot determine timezone using google: {requestUri}\n{e}");
                return null;
            }
        }
    }
}
