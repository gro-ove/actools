using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Shaders;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapRenderHelper : IRenderHelper {
        private EffectSpecialTrackMap _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectSpecialTrackMap>();
        }

        public void OnResize(DeviceContextHolder holder) { }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView viewBlurred, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.FxBlurredMap.SetResource(viewBlurred);
            _effect.TechPp.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Final(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, bool checkedBackground) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxPreprocessedMap.SetResource(view);
            (checkedBackground ? _effect.TechFinalCheckers : _effect.TechFinal).DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}