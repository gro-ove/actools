using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class DownsampleHelper : IRenderHelper {
        private EffectPpDownsample _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDownsample>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture destination) {
            holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0f, 0f, destination.Width, destination.Height));

            holder.DeviceContext.OutputMerger.SetTargets(destination.TargetView);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(source.View);

            _effect.FxScreenSize.Set(new Vector4(destination.Width, destination.Height, 1f / destination.Width, 1f / destination.Height));
            _effect.FxMultipler.Set(new Vector2((float)destination.Width / source.Width, (float)destination.Height / source.Height));

            _effect.TechAverage.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}