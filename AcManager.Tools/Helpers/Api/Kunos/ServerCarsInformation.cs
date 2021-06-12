using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerCarsInformation {
        [JsonProperty(PropertyName = "Features"), CanBeNull]
        public string[] Features { get; set; }

        [JsonProperty(PropertyName = "Cars")]
        public ServerActualCarInformation[] Cars { get; set; }
    }
}
