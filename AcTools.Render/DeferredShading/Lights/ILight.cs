using AcTools.Render.Base.Shaders;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.Lights {
    public enum SpecialLightMode {
        Default, Shadows, ShadowsWithoutFilter, Debug
    }

    public interface ILight {
        void Draw(DeviceContext deviceContext, EffectDeferredLight lighting, SpecialLightMode mode);
    }
}
