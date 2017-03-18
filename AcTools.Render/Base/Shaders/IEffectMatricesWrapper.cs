using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    public interface IEffectMatricesWrapper {
        EffectOnlyMatrixVariable FxWorld { get; }
        EffectOnlyMatrixVariable FxWorldInvTranspose { get; }
        EffectOnlyMatrixVariable FxWorldViewProj { get; }
    }

    // To make it more type-strict (and avoid losing tons of hours because of accidental “Set()” instead of “SetMatrix()” in the future! Arghh…)
    public class EffectOnlyMatrixVariable {
        private readonly EffectMatrixVariable _v;

        public EffectOnlyMatrixVariable(EffectMatrixVariable v) {
            _v = v;
        }

        public void SetMatrix(Matrix m) {
            _v.SetMatrix(m);
        }
    }

    public class EffectOnlyMatrixArrayVariable {
        private readonly EffectMatrixVariable _v;

        public EffectOnlyMatrixArrayVariable(EffectMatrixVariable v) {
            _v = v;
        }

        public void SetMatrixArray(Matrix[] m) {
            _v.SetMatrixArray(m);
        }

        public void SetMatrixArray(Matrix[] m, int offset, int count) {
            _v.SetMatrixArray(m, offset, count);
        }
    }
}