using System.ComponentModel;

namespace AcManager.Tools.Objects {
    public enum ServerPresetDetailsMode {
        [Description("Via ID in name (recommended)")]
        ViaNameIdentifier = 1,

        [Description("Via AC Server Wrapper")]
        ViaWrapper = 2,
    }
}