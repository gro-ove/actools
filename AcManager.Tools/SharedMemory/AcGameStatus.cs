using System.ComponentModel;

namespace AcManager.Tools.SharedMemory {
    public class AcShared {
        public AcShared(AcSharedPhysics physics, AcSharedGraphics graphics, AcSharedStaticInfo staticInfo) {
            Physics = physics;
            Graphics = graphics;
            StaticInfo = staticInfo;
        }

        public AcSharedPhysics Physics { get; }

        public AcSharedGraphics Graphics { get; }

        public AcSharedStaticInfo StaticInfo { get; }
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