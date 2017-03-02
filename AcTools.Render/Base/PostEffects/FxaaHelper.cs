using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
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
            Draw(holder, view, target, new Vector2(holder.Width, holder.Height));
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, Vector2 size) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxScreenSize.Set(new Vector4(size.X, size.Y, 1f / size.X, 1f / size.Y));
            _effect.FxInputMap.SetResource(view);
            _effect.TechFxaa.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}