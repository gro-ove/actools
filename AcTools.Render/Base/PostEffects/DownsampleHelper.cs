using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class DownsampleHelper : IRenderHelper {
        private EffectPpDownsample _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDownsample>();
        }

        public void OnResize(DeviceContextHolder holder) { }

        private void Draw(DeviceContextHolder holder, ShaderResourceView source, Vector2 sourceSize, RenderTargetView destination, Vector2 destinationSize, EffectReadyTechnique tech) {
            holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0f, 0f, destinationSize.X, destinationSize.Y));

            holder.DeviceContext.OutputMerger.SetTargets(destination);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(source);

            _effect.FxScreenSize.Set(new Vector4(destinationSize.X, destinationSize.Y, 1f / destinationSize.X, 1f / destinationSize.Y));
            _effect.FxMultipler.Set(new Vector2(destinationSize.X / sourceSize.X, destinationSize.Y / sourceSize.Y));

            tech.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Draw(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture destination) {
            Draw(holder, source.View, new Vector2(source.Width, source.Height), destination.TargetView, new Vector2(destination.Width, destination.Height),
                    _effect.TechAverage);
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView source, Vector2 sourceSize, RenderTargetView destination, Vector2 destinationSize) {
            Draw(holder, source, sourceSize, destination, destinationSize, _effect.TechAverage);
        }

        /*public void Bicubic(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture destination) {
            Draw(holder, source.View, new Vector2(source.Width, source.Height), destination.TargetView, new Vector2(destination.Width, destination.Height),
                    _effect.TechBicubic);
        }

        public void Lanczos(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture destination) {
            Draw(holder, source.View, new Vector2(source.Width, source.Height), destination.TargetView, new Vector2(destination.Width, destination.Height),
                    _effect.TechLanczos);
        }*/

        public void Dispose() {}
    }
}