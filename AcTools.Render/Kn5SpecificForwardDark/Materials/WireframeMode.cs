using System.ComponentModel;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public enum WireframeMode {
        [Description("Solid")]
        Disabled,

        [Description("Combined")]
        Filled,

        [Description("Wireframe only")]
        LinesOnly
    }
}