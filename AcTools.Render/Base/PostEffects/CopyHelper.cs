using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
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

        public void Dispose() {}
    }
}