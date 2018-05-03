using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapBlurRenderHelper : IRenderHelper {
        private EffectSpecialTrackMap _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectSpecialTrackMap>();
        }

        public void OnResize(DeviceContextHolder holder) {
            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            BlurHorizontally(holder, view);
        }

        public void BlurHorizontally(DeviceContextHolder holder, ShaderResourceView view) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechPpHorizontalBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void BlurVertically(DeviceContextHolder holder, ShaderResourceView view) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechPpVerticalBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Blur(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, int iterations = 1,
                TargetResourceTexture target = null) {
            for (var i = 0; i < iterations; i++) {
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View);
                holder.DeviceContext.OutputMerger.SetTargets(target?.TargetView ?? source.TargetView);
                BlurVertically(holder, temporary.View);
            }
        }

        public void Dispose() { }
    }
}