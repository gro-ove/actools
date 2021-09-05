using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.AcPlugins.Extras {
    [JsonObject(MemberSerialization.OptIn)]
    public class AcDriverLocation : NotifyPropertyChanged {
        [JsonConstructor]
        private AcDriverLocation() { }

        public AcDriverLocation(float x, float z) {
            PositionX = x;
            PositionZ = z;
        }

        [JsonProperty("x")]
        public float PositionX { get; set; }

        [JsonProperty("z")]
        public float PositionZ { get; set; }
    }
}