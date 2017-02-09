using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;

namespace ArcadeCorsa.Render.DarkRenderer.Materials {
    public class FlatMirrorMaterialSimple : IRenderableMaterial {
        private EffectDarkMaterial _effect;

        internal FlatMirrorMaterialSimple() {}

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDarkMaterial>();
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent) return false;
            
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPT;

            if (mode != SpecialRenderMode.Outline) {
                contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.LessEqualReadOnlyDepthState;
            }

            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechFlatMirror.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }

        public bool IsBlending => true;

        public void Dispose() {}
    }
}