using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Textures;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Materials {
    public class Kn5RenderableSpecialGlMaterial : IRenderableMaterial {
        private EffectDeferredGObjectSpecial _effect;

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObjectSpecial>();
        }

        public void Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform*camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            (mode == SpecialRenderMode.Default ? _effect.TechSpecialGlDeferred : _effect.TechSpecialGlForward)
                .DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() {
        }
    }
}
