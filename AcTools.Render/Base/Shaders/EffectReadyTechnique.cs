using System.Runtime.CompilerServices;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
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