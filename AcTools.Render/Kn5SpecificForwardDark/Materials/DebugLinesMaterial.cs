using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class DebugLinesMaterial : IRenderableMaterial {
        private EffectSpecialDebugLines _effect;

        internal DebugLinesMaterial() { }

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSpecialDebugLines>();
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPC;
            contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.DisabledDepthState;
            contextHolder.DeviceContext.Rasterizer.State = contextHolder.States.DoubleSidedState;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            // _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
            contextHolder.DeviceContext.Rasterizer.State = null;
        }

        public bool IsBlending => true;

        public void Dispose() { }
    }
}