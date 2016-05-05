using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using SlimDX;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class Kn5MaterialDepth : IRenderableMaterial {
        private EffectSimpleMaterial _effect;

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSimpleMaterial>();
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple) return false;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechGl.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() {}
    }
}