using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class Fxaa311Helper : IRenderHelper {
        private EffectPpFxaa311 _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpFxaa311>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, TargetResourceTexture temporaryLuma) {
            holder.PrepareQuad(_effect.LayoutPT);

            holder.DeviceContext.OutputMerger.SetTargets(temporaryLuma.TargetView);
            _effect.FxInputMap.SetResource(view);
            _effect.TechLuma.DrawAllPasses(holder.DeviceContext, 6);

            holder.DeviceContext.OutputMerger.SetTargets(target);
            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
            _effect.FxInputMap.SetResource(temporaryLuma.View);
            _effect.TechFxaa.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {
        }
    }
}