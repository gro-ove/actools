using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerInformationExtra : IServerInformationExtra {
        /// <summary>
        /// Name and ID.
        /// </summary>
        [JsonProperty(PropertyName = "country")]
        public string[] Country { get; set; }

        [JsonProperty(PropertyName = "city")]
        public string City { get; set; }

        /// <summary>
        /// In seconds.
        /// </summary>
        [JsonProperty(PropertyName = "durations")]
        public long[] Durations { get; set; }

        /// <summary>
        /// Usual and admin passwords.
        /// </summary>
        [JsonProperty(PropertyName = "passwordChecksum")]
        public string[] PasswordChecksum { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [JsonProperty(PropertyName = "players")]
        public ServerCarsInformation Players { get; set; }

        [JsonProperty(PropertyName = "assists")]
        public ServerInformationExtendedAssists Assists { get; set; }

        [JsonProperty(PropertyName = "contentPrivate")]
        public string ContentPrivate { get; set; }

        [JsonProperty(PropertyName = "content")]
        public JObject Content { get; set; }

        [JsonProperty(PropertyName = "trackBase")]
        public string TrackBase { get; set; }

        [JsonProperty(PropertyName = "currentWeatherId")]
        public string WeatherId { get; set; }

        [JsonProperty(PropertyName = "frequency")]
        public int? FrequencyHz { get; set; }

        [JsonProperty(PropertyName = "ambientTemperature")]
        public double? AmbientTemperature { get; set; }

        [JsonProperty(PropertyName = "roadTemperature")]
        public double? RoadTemperature { get; set; }

        [JsonProperty(PropertyName = "windSpeed")]
        public double? WindSpeed { get; set; }

        [JsonProperty(PropertyName = "windDirection")]
        public double? WindDirection { get; set; }

        [JsonProperty(PropertyName = "grip")]
        public double? Grip { get; set; }

        [JsonProperty(PropertyName = "gripTransfer")]
        public double? GripTransfer { get; set; }

        [JsonProperty(PropertyName = "maxContactsPerKm")]
        public double? MaxContactsPerKm { get; set; }
    }
}