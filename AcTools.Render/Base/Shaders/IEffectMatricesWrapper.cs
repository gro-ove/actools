using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    public interface IEffectMatricesWrapper {
        EffectMatrixVariable FxWorld { get; }
        EffectMatrixVariable FxWorldInvTranspose { get; }
        EffectMatrixVariable FxWorldViewProj { get; }
    }
}