using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class FxaaHelper : IRenderHelper {
        private EffectPpBasic _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpBasic>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
            _effect.FxInputMap.SetResource(view);
            _effect.TechFxaa.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}