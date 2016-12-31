using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    public interface IEffectScreenSizeWrapper {
        EffectVectorVariable FxScreenSize { get; }
    }
}