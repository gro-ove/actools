using System;
using System.ComponentModel;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.Api.Kunos {
    [Localizable(false)]
    public class ServerInformationExtended : ServerInformationComplete {
        [JsonProperty(PropertyName = "wrappedPort")]
        public int PortExtended { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "city")]
        public string City { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "passwordChecksum")]
        public string[] PasswordChecksum { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "players")]
        public ServerCarsInformation Players { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "assists")]
        public ServerInformationExtendedAssists Assists { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "content")]
        public JObject Content { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "trackBase")]
        public string TrackBase { get; set; }

        [CanBeNull, JsonProperty(PropertyName = "currentWeatherId")]
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

        [JsonProperty(PropertyName = "until")]
        public long Until { get; set; }

        [JsonIgnore]
        public DateTime UntilLocal { get; set; }
    }
}