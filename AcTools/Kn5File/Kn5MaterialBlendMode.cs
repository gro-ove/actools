using System.ComponentModel;

namespace AcTools.Kn5File {
    public enum Kn5MaterialBlendMode {
        [Description("Opaque")]
        Opaque = 0,

        [Description("Alpha blend")]
        AlphaBlend = 1,

        [Description("Alpha to coverage")]
        AlphaToCoverage = 2
    }
}