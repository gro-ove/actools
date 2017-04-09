using System.Runtime.CompilerServices;
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

    public class EffectOnlyResourceVariable {
        private readonly EffectResourceVariable _v;

        public EffectOnlyResourceVariable(EffectResourceVariable v) {
            _v = v;
        }

        public void SetResource(ShaderResourceView m) {
            _v.SetResource(m);
        }
    }

    public class EffectOnlyResourceArrayVariable {
        private readonly EffectResourceVariable _v;

        public EffectOnlyResourceArrayVariable(EffectResourceVariable v) {
            _v = v;
        }

        public void SetResourceArray(ShaderResourceView[] m) {
            _v.SetResourceArray(m);
        }

        public void SetResourceArray(ShaderResourceView[] m, int offset, int count) {
            _v.SetResourceArray(m, offset, count);
        }
    }

    public class EffectReadyTechnique {
        private readonly EffectTechnique _t;
        private int _passCount = -1;
        private EffectPass[] _passes;

        public EffectReadyTechnique(EffectTechnique t) {
            _t = t;
        }

        public EffectReadyTechnique Description => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Initialize() {
            if (_passCount < 0) {
                _passCount = _t.Description.PassCount;
                _passes = new EffectPass[_passCount];
                for (var i = 0; i < _passCount; i++) {
                    _passes[i] = _t.GetPassByIndex(i);
                }
            }
        }

        public int PassCount {
            get {
                Initialize();
                return _passCount;
            }
        }

        public void DrawAllPasses(DeviceContext context, int indexCount) {
            Initialize();
            for (var i = 0; i < _passCount; i++) {
                _passes[i].Apply(context);
                context.DrawIndexed(indexCount, 0, 0);
            }
        }

        public EffectPass GetPassByIndex(int index) {
            Initialize();
            return _passes[index];
        }
    }
}