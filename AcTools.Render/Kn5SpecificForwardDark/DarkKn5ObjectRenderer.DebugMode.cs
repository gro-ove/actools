using AcTools.Render.Shaders;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer {
        private static bool IsInDebugMode() {
#pragma warning disable 162
            return EffectDarkMaterial.DebugMode;
#pragma warning restore 162
        }
    }
}