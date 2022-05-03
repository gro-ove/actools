using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleMirror : IRenderableMaterial, IAcDynamicMaterial {
        public bool IsBlending => false;

        public string Name => "!__mirror__";

        private EffectSimpleMaterial _effect;

        internal Kn5MaterialSimpleMirror() {}

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSimpleMaterial>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {
            // Because Dispose() is empty, we can just re-initialize shader
            _effect = null;
            EnsureInitialized(contextHolder);
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!mode.HasFlag(SpecialRenderMode.Simple)) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
             _effect.TechMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() { }

        void IAcDynamicMaterial.SetEmissiveNext(Vector3 value, float multipler) { }

        void IAcDynamicMaterial.SetRadialSpeedBlurNext(float amount) { }
    }
}
