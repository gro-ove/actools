using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleMirror : IRenderableMaterial, IEmissiveMaterial {
        public bool IsBlending => false;
        
        private EffectSimpleMaterial _effect;

        internal Kn5MaterialSimpleMirror() {}

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSimpleMaterial>();
        }

        public void SetEmissive(Vector3 value) {}

        public void SetEmissiveNext(Vector3 value) {}

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!mode.HasFlag(SpecialRenderMode.Simple)) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
             _effect.TechMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() {}
    }
}
