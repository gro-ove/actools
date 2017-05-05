using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    public interface IEffectScreenSizeWrapper {
        EffectOnlyVector4Variable FxScreenSize { get; }
    }
}