using System;
using System.Net;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public class IpGeoEntry {
        [JsonProperty(@"ip")]
        public string Ip;

        [JsonProperty(@"city")]
        public string City;

        [JsonProperty(@"region")]
        public string Region;

        [JsonProperty(@"country")]
        public string Country;

        [JsonProperty(@"loc")]
        public string Location;

        [JsonProperty(@"postal")]
        public string Postal;
    }

    public static class IpGeoProvider {
        private const string RequestUri = "http://ipinfo.io/geo";

        [CanBeNull]
        public static IpGeoEntry Get() {
            if ((DateTime.Now - CacheStorage.Get<DateTime>(@".IpGeoInformation.Time")).TotalDays < 1d) {
                return CacheStorage.Storage.GetObject<IpGeoEntry>(".IpGeoInformation");
            }

            const string requestUri = RequestUri;

            IpGeoEntry result;
            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";
                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    result = response.StatusCode != HttpStatusCode.OK
                            ? null : JsonConvert.DeserializeObject<IpGeoEntry>(response.GetResponseStream()?.ReadAsStringAndDispose());
                }
            } catch (Exception e) {
                Logging.Warning($"Cannot determine location: {requestUri}\n{e}");
                result = null;
            }

            CacheStorage.Storage.SetObject(".IpGeoInformation", result);
            CacheStorage.Storage.SetObject(".IpGeoInformation.Time", DateTime.Now);
            return result;
        }
    }
}