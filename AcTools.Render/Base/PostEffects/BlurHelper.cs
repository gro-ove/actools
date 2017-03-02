using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

using GaussianSize = System.Tuple<int, int, float>;
using GaussianHW = System.Tuple<AcTools.Render.Base.PostEffects.BlurHelper.BlurGaussian, AcTools.Render.Base.PostEffects.BlurHelper.BlurGaussian>;

namespace AcTools.Render.Base.PostEffects {
    // TODO: Move specific functions outside! Maybe even, use a different shader for them.
    public class BlurHelper : IRenderHelper {
        private EffectPpBlur _effect;

        public class BlurGaussian {
            public readonly float[] Weights;
            public readonly Vector4[] Offsets;

            private static float ComputeGaussian(float n, float theta) {
                return (-n * n / (2.0f * theta * theta)).Exp() / (2.0f * MathF.PI * theta).Sqrt();
            }

            public BlurGaussian(float dx, float dy, float force) {
                // Look up how many samples our gaussian blur effect supports.
                var sampleCount = EffectPpBlur.SampleCount;

                // Create temporary arrays for computing our filter settings.
                var sampleWeights = new float[sampleCount];
                var sampleOffsets = new Vector4[sampleCount];

                // The first sample always has a zero offset.
                sampleWeights[0] = ComputeGaussian(0, force);
                sampleOffsets[0] = new Vector4(0);

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

                    var delta = new Vector4(dx, dy, 0, 0) * sampleOffset;

                    // Store texture coordinate offsets for the positive and negative taps.
                    sampleOffsets[i * 2 + 1] = delta;
                    sampleOffsets[i * 2 + 2] = -delta;
                }

                // Normalize the list of sample weightings, so they will always sum to one.
                for (var i = 0; i < sampleWeights.Length; i++) {
                    sampleWeights[i] /= totalWeights;
                }

                // Tell the effect about our new filter settings.
                Weights = sampleWeights;
                Offsets = sampleOffsets;
            }
        }

        private BlurGaussian _h, _v;

        private readonly Dictionary<GaussianSize, GaussianHW> _cache = new Dictionary<GaussianSize, GaussianHW>(10);

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpBlur>();
        }

        public void OnResize(DeviceContextHolder holder) {
            _cache.Clear();
        }

        private int _width, _height;

        private void Resize(int width, int height, float blurLevel) {
            if (width == _width && _height == height) return;

            _width = width;
            _height = height;

            var size = new GaussianSize(width, height, blurLevel);
            GaussianHW cached;
            if (!_cache.TryGetValue(size, out cached)) {
                cached = new GaussianHW(new BlurGaussian(1f / width, 0, blurLevel), new BlurGaussian(0, 1f / height, blurLevel));
                _cache[size] = cached;
            }

            _h = cached.Item1;
            _v = cached.Item2;
        }

        private void PrepareHorizontal() {
            _effect.FxSampleOffsets.Set(_h.Offsets);
            _effect.FxSampleWeights.Set(_h.Weights);
        }

        private void PrepareVertical() {
            _effect.FxSampleOffsets.Set(_v.Offsets);
            _effect.FxSampleWeights.Set(_v.Weights);
        }

        private void BlurHorizontally(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            PrepareHorizontal();
            _effect.FxPower.Set(power);
            _effect.TechGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void BlurVertically(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            PrepareVertical();
            _effect.FxPower.Set(power);
            _effect.TechGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be set accordingly to source and temporary params.
        /// </summary>
        public void Blur(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, float power = 1f, int iterations = 1,
                TargetResourceTexture target = null) {
            for (var i = 0; i < iterations; i++) {
                Resize(temporary.Width, temporary.Height, 8f);
                holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View, power);

                if (target != null) {
                    Resize(target.Width, target.Height, 8f);
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
            _effect.FxPower.Set(power);
            _effect.TechFlatMirrorBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void BlurFlatMirrorVertically(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
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
                holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurFlatMirrorHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View, power);

                holder.DeviceContext.Rasterizer.SetViewports(actualTarget.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(actualTarget.TargetView);
                BlurFlatMirrorVertically(holder, temporary.View, power);
            }
        }
        #endregion

        #region Dark SSLR-specific
        private void BlurDarkSslrHorizontally(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            PrepareHorizontal();
            _effect.FxPower.Set(power);
            _effect.TechDarkSslrBlur0.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void BlurDarkSslrVertically(DeviceContextHolder holder, ShaderResourceView view, float power) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            PrepareVertical();
            _effect.FxPower.Set(power);
            _effect.TechDarkSslrBlur0.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be set accordingly to source and temporary params.
        /// </summary>
        public void BlurDarkSslr(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, float power = 1f, int iterations = 1,
                TargetResourceTexture target = null) {
            for (var i = 0; i < iterations; i++) {
                Resize(temporary.Width, temporary.Height, 8f);
                holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurDarkSslrHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View, power);

                if (target != null) {
                    Resize(target.Width, target.Height, 8f);
                } else {
                    Resize(source.Width, source.Height, 8f);
                }

                holder.DeviceContext.Rasterizer.SetViewports(target?.Viewport ?? source.Viewport);
                holder.DeviceContext.OutputMerger.SetTargets(target?.TargetView ?? source.TargetView);
                BlurDarkSslrVertically(holder, temporary.View, power);
            }
        }
        #endregion

        /// <summary>
        /// Width and height will be taken from DeviceContextHolder.
        /// </summary>
        public void BlurReflectionHorizontally(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView mapsView) {
            Resize(holder.Width, holder.Height, 8f);

            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxMapsMap.SetResource(mapsView);
            PrepareHorizontal();
            _effect.FxPower.Set(0.1f);
            _effect.TechReflectionGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        /// <summary>
        /// Width and height will be taken from DeviceContextHolder.
        /// </summary>
        public void BlurReflectionVertically(DeviceContextHolder holder, ShaderResourceView view, ShaderResourceView mapsView) {
            Resize(holder.Width, holder.Height, 8f);

            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            _effect.FxInputMap.SetResource(view);
            _effect.FxMapsMap.SetResource(mapsView);
            PrepareVertical();
            _effect.FxPower.Set(0.1f);
            _effect.TechReflectionGaussianBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}
