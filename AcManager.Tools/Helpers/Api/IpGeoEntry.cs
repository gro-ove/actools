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
}