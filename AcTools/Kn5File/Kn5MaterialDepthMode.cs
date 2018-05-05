using System.ComponentModel;

namespace AcTools.Kn5File {
    public enum Kn5MaterialDepthMode {
        [Description("Normal")]
        DepthNormal = 0,

        [Description("Read-only")]
        DepthNoWrite = 1,

        [Description("Off")]
        DepthOff = 2
    }
}