using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api.Kunos {
    public class ServerActualCarInformation : IWithId {
        [JsonProperty(PropertyName = "Model"), CanBeNull]
        public string CarId { get; set; }

        [JsonProperty(PropertyName = "Skin"), CanBeNull]
        public string CarSkinIdRaw { get; set; }

        [JsonIgnore]
        private string _carSkinId;

        [JsonIgnore]
        private string _cspParams;

        private void SplitRawSkinId() {
            if (_carSkinId == null) {
                if (CarSkinIdRaw == null) {
                    _carSkinId = "";
                } else {
                    var index = CarSkinIdRaw.IndexOf('/');
                    _carSkinId = index == -1 ? CarSkinIdRaw : CarSkinIdRaw.Substring(0, index);
                    _cspParams = index == -1 ? null : CarSkinIdRaw.Substring(index + 1);
                }
            }
        }

        [JsonIgnore, NotNull]
        public string CarSkinId {
            get {
                SplitRawSkinId();
                return _carSkinId;
            }
        }

        [JsonIgnore, CanBeNull]
        public string CspParams {
            get {
                SplitRawSkinId();
                return _cspParams;
            }
        }

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
