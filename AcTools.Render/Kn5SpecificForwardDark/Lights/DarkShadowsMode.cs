using AcTools.Render.Shaders;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public enum DarkShadowsMode : uint {
        Off = EffectDarkMaterial.LightShadowOff,
        Main = EffectDarkMaterial.LightShadowMain,
        ExtraSmooth = EffectDarkMaterial.LightShadowExtra,
        ExtraFast = EffectDarkMaterial.LightShadowExtraFast,
        ExtraPoint = EffectDarkMaterial.LightShadowExtraCube,
    }
}