using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerCarsInformation {
        [JsonProperty(PropertyName = "Cars")]
        public ServerActualCarInformation[] Cars { get; set; }
    }
}
