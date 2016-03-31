using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerActualInformation {
        [JsonProperty(PropertyName = "Cars")]
        public ServerActualCarInformation[] Cars { get; set; }
    }
}
