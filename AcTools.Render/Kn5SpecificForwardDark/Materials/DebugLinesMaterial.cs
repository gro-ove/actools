using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class DebugLinesMaterial : IRenderableMaterial {
        private EffectSpecialDebugLines _effect;

        internal DebugLinesMaterial() { }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSpecialDebugLines>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        private BlendState _blendState;
        private DepthStencilState _depthState;
        private RasterizerState _rasterizerState;

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline) return false;

            _blendState = contextHolder.DeviceContext.OutputMerger.BlendState;
            _depthState = contextHolder.DeviceContext.OutputMerger.DepthStencilState;
            _rasterizerState = contextHolder.DeviceContext.Rasterizer.State;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPC;
            contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.DisabledDepthState;
            contextHolder.DeviceContext.Rasterizer.State = contextHolder.States.DoubleSidedSmoothLinesState;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            // _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.FxOverrideColor.Set(mode == SpecialRenderMode.Outline);
            if (mode == SpecialRenderMode.Outline) {
                _effect.FxCustomColor.Set(new Vector4(1f));
            }

            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = _blendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = _depthState;
            contextHolder.DeviceContext.Rasterizer.State = _rasterizerState;
        }

        public bool IsBlending => true;

        public void Dispose() { }
    }
}