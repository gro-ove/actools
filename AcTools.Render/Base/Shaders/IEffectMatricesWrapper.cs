namespace AcTools.Render.Base.Shaders {
    public interface IEffectMatricesWrapper {
        EffectOnlyMatrixVariable FxWorld { get; }
        EffectOnlyMatrixVariable FxWorldInvTranspose { get; }
        EffectOnlyMatrixVariable FxWorldViewProj { get; }
    }
}