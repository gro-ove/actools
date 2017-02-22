using System.Linq;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class BlurHelper : IRenderHelper {
        private EffectPpBlur _effect;

        private float[] _hosw;
        private Vector4[] _hoso;
        private float[] _vosw;
        private Vector4[] _voso;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpBlur>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        private int _width, _height;

        private void Resize(int width, int height) {
            if (width == _width && _height == height) return;

            _width = width;
            _height = height;

            var bloomBlur = 8f;
            CalculateGaussian(1f / width, 0, bloomBlur, out _hosw, out _hoso);
            CalculateGaussian(0, 1f / height, bloomBlur, out _vosw, out _voso);
        }

        private static float ComputeGaussian(float n, float theta) {
            return (-n * n / (2.0f * theta * theta)).Exp() / (2.0f * MathF.PI * theta).Sqrt();
        }

        private static void CalculateGaussian(float dx, float dy, float force, out float[] weightsParameter, out Vector4[] offsetsParameter) {
            // Look up how many samples our gaussian blur effect supports.
            const int sampleCount = EffectPpBlur.SampleCount;

            // Create temporary arrays for computing our filter settings.
            var sampleWeights = new float[sampleCount];
            var sampleOffsets = new Vector2[sampleCount];

            // The first sample always has a zero offset.
            sampleWeights[0] = ComputeGaussian(0, force);
            sampleOffsets[0] = new Vector2(0);

            // Maintain a sum of all the weighting values.
            var totalWeights = sampleWeights[0];

            // Add pairs of additional sample taps, positioned
            // along a line in both directions from the center.
            for (var i = 0; i < sampleCount / 2; i++) {
                // Store weights for the positive and negative taps.
                var weight = ComputeGaussian(i + 1, force);

                sampleWeights[i * 2 + 1] = weight;
                sampleWeights[i * 2 + 2] = weight;

                totalWeights += weight * 2;

                // To get the maximum amount of blurring from a limited number of
                // pixel shader samples, we take advantage of the bilinear filtering
                // hardware inside the texture fetch unit. If we position our texture
                // coordinates exactly halfway between two texels, the filtering unit
                // will average them for us, giving two samples for the price of one.
                // This allows us to step in units of two texels per sample, rather
                // than just one at a time. The 1.5 offset kicks things off by
                // positioning us nicely in between two texels.
                var sampleOffset = i * 2 + 1.5f;

                var delta = new Vector2(dx, dy) * sampleOffset;

                // Store texture coordinate offsets for the positive and negative taps.
                sampleOffsets[i * 2 + 1] = delta;
                sampleOffsets[i * 2 + 2] = -delta;
            }

            // Normalize the list of sample weightings, so they will always sum to one.
            for (var i = 0; i < sampleWeights.Length; i++) {
                sampleWeights[i] /= totalWeights;
            }

            // Tell the effect about our new filter settings.
            weightsParameter = sampleWeights;
            offsetsParameter = sampleOffsets.Select(x => new Vector4(x, 0, 0)).ToArray();
        }

        private void BlurHorizontally(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxSampleOffsets.Set(_hoso);
            _effect.FxSampleWeights.Set(_hosw);
            _effect.FxPower.Set(power);
            _effect.TechGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void BlurVertically(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxSampleOffsets.Set(_voso);
            _effect.FxSampleWeights.Set(_vosw);
            _effect.FxPower.Set(power);
            _effect.TechGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be set accordingly to source and temporary params.
        /// </summary>
        public void Blur(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, float power = 1f, int iterations = 1,
                TargetResourceTexture target = null) {
            for (var i = 0; i < iterations; i++) {
                Resize(temporary.Width, temporary.Height);
                holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View, power);

                if (target != null) {
                    Resize(target.Width, target.Height);
                }

                holder.DeviceContext.Rasterizer.SetViewports(target?.Viewport ?? source.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(target?.TargetView ?? source.TargetView);
                BlurVertically(holder, temporary.View, power);
            }
        }

        #region Flat mirror
        private void BlurFlatMirrorHorizontally(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxSampleOffsets.Set(_hoso);
            _effect.FxSampleWeights.Set(_hosw);
            _effect.FxPower.Set(power);
            _effect.TechFlatMirrorBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void BlurFlatMirrorVertically(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxSampleOffsets.Set(_voso);
            _effect.FxSampleWeights.Set(_vosw);
            _effect.FxPower.Set(power);
            _effect.TechFlatMirrorBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be set accordingly to source and temporary params.
        /// </summary>
        public void BlurFlatMirror(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, Matrix viewProjInv,
                ShaderResourceView depth, float power = 1f, int iterations = 1, TargetResourceTexture target = null) {
            _effect.FxFlatMirrorDepthMap.SetResource(depth);
            _effect.FxWorldViewProjInv.SetMatrix(viewProjInv);

            var actualTarget = target ?? source;
            _effect.FxScreenSize.Set(new Vector4(actualTarget.Width, actualTarget.Height, 1f / actualTarget.Width, 1f / actualTarget.Height));

            for (var i = 0; i < iterations; i++) {
                Resize(actualTarget.Width, actualTarget.Height);
                holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurFlatMirrorHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View, power);

                if (target != null) {
                    Resize(target.Width, target.Height);
                }

                holder.DeviceContext.Rasterizer.SetViewports(actualTarget.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(actualTarget.TargetView);
                BlurFlatMirrorVertically(holder, temporary.View, power);
            }
        }
        #endregion

        /// <summary>
        /// Width and height will be taken from DeviceContextHolder.
        /// </summary>
        public void BlurReflectionHorizontally(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView mapsView) {
            Resize(holder.Width, holder.Height);

            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxMapsMap.SetResource(mapsView);
            _effect.FxSampleOffsets.Set(_hoso);
            _effect.FxSampleWeights.Set(_hosw);
            _effect.FxPower.Set(0.1f);
            _effect.TechReflectionGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be taken from DeviceContextHolder.
        /// </summary>
        public void BlurReflectionVertically(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView mapsView) {
            Resize(holder.Width, holder.Height);

            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxMapsMap.SetResource(mapsView);
            _effect.FxSampleOffsets.Set(_voso);
            _effect.FxSampleWeights.Set(_vosw);
            _effect.FxPower.Set(0.1f);
            _effect.TechReflectionGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}
