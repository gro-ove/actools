using AcTools.Render.Base.Utils;
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
            Draw(holder, view, target, false);
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, bool hq) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);

            if (hq) {
                _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
            }

            (hq ? _effect.TechCopyHq : _effect.TechCopy).DrawAllPasses(holder.DeviceContext, 6);
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