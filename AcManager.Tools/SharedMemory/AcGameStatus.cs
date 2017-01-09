using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AcManager.Tools.SharedMemory {
    public class AcShared : IJsonSerializable {
        public AcShared(AcSharedPhysics physics, AcSharedGraphics graphics, AcSharedStaticInfo staticInfo) {
            Physics = physics;
            Graphics = graphics;
            StaticInfo = staticInfo;
        }

        public AcSharedPhysics Physics { get; }

        public AcSharedGraphics Graphics { get; }

        public AcSharedStaticInfo StaticInfo { get; }

        string IJsonSerializable.ToJson() {
            return JsonConvert.SerializeObject(this, Formatting.None, new JsonSerializerSettings {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.None
            });
        }
    }

    public enum AcGameStatus {
        [Description("Off")]
        AcOff = 0,

        [Description("Replay")]
        AcReplay = 1,

        [Description("Live")]
        AcLive = 2,

        [Description("Pause")]
        AcPause = 3
    }
}