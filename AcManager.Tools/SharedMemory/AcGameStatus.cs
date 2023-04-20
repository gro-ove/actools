using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AcManager.Tools.SharedMemory {
    public class AcShared : IJsonSerializable {
        private readonly BetterMemoryMappedAccessor<AcSharedStaticInfo> _staticInfo;

        public AcShared(AcSharedPhysics physics, AcSharedGraphics graphics, BetterMemoryMappedAccessor<AcSharedStaticInfo> staticInfo) {
            _staticInfo = staticInfo;
            Physics = physics;
            Graphics = graphics;
        }

        public AcSharedPhysics Physics { get; }

        public AcSharedGraphics Graphics { get; }

        private AcSharedStaticInfo _staticInfoData;

        public AcSharedStaticInfo StaticInfo => _staticInfoData ?? (_staticInfoData = _staticInfo.Get());

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