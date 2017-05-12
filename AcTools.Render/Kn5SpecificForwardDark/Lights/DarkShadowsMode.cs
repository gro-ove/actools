using AcTools.Render.Shaders;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public enum DarkShadowsMode : uint {
        Off = EffectDarkMaterial.LightShadowOff,
        Main = EffectDarkMaterial.LightShadowMain,
        ExtraSmooth = EffectDarkMaterial.LightShadowExtraSmooth,
        ExtraFast = EffectDarkMaterial.LightShadowExtraFast
    }
}