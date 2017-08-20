using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class CopyHelper : IRenderHelper {
        private EffectPpBasic _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpBasic>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechCopy.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void DrawSqr(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechCopySqr.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void DrawSqrt(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechCopySqrt.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Draw(DeviceContextHolder holder, TargetResourceDepthTexture from, TargetResourceTexture to) {
            holder.DeviceContext.Rasterizer.SetViewports(to.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(to.TargetView);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(from.View);
            _effect.TechCopy.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void DepthToLinear(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, float zNear, float zFar,
                float zFarNormalize) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.FxScreenSize.Set(new Vector4(zNear, zFar, zFarNormalize, 0));
            _effect.TechDepthToLinear.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void AccumulateDivide(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, int amount) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.FxMultipler.Set(1f / amount);
            _effect.TechAccumulateDivide.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void AccumulateBokehDivide(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView maxView, RenderTargetView target, int amount,
                float bokehMultiplier) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.FxOverlayMap.SetResource(maxView);
            _effect.FxMultipler.Set(1f / amount);
            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
            _effect.FxBokenMultipler.Set(new Vector2(bokehMultiplier, 1f - bokehMultiplier));
            _effect.TechAccumulateBokehDivide.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Cut(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, float cut) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.FxSizeMultipler.Set(1f / cut);
            _effect.TechCut.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}