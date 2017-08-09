using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerActualCarInformation : IWithId {
        [JsonProperty(PropertyName = "Model")]
        public string CarId { get; set; }

        [JsonProperty(PropertyName = "Skin")]
        public string CarSkinId { get; set; }

        [JsonProperty(PropertyName = "DriverName")]
        public string DriverName { get; set; }

        [JsonProperty(PropertyName = "DriverTeam")]
        public string DriverTeam { get; set; }

        [JsonProperty(PropertyName = "IsConnected")]
        public bool IsConnected { get; set; }

        /// <summary>
        /// True if this slot was booked for a player.
        /// </summary>
        [JsonProperty(PropertyName = "IsRequestedGUID")]
        public bool IsRequestedGuid { get; set; }

        [JsonProperty(PropertyName = "IsEntryList")]
        public bool IsEntryList { get; set; }

        [JsonIgnore]
        string IWithId<string>.Id => CarId;
    }
}
