using System;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkSsaoHelper : IRenderHelper {
        private EffectPpSsao _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpSsao>();

            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(4, 4));

            var samplesKernel = new Vector4[EffectPpSsao.SampleCount];
            for (var i = 0; i < samplesKernel.Length; i++) {
                samplesKernel[i].X = MathUtils.Random(-1f, 1f);
                samplesKernel[i].Z = MathUtils.Random(-1f, 1f);
                samplesKernel[i].Y = MathUtils.Random(0f, 1f);
                samplesKernel[i].Normalize();

                var scale = (float)i / samplesKernel.Length;
                scale = MathUtils.Lerp(0.1f, 1.0f, scale * scale);
                samplesKernel[i] *= scale;
            }

            _effect.FxSamplesKernel.Set(samplesKernel);
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target) {
            _effect.FxNoiseSize.Set(new Vector2(holder.Width / 4f, holder.Height / 4f));

            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSsao.DrawAllPasses(holder.DeviceContext, 6);
        }

        private float[] _gaussianBlur;

        private static float[] GaussianBlur() {
            var radius = EffectPpSsao.BlurRadius;
            var weights = new float[radius * 2 + 1];

            var sqrtTwoPiTimesRadiusRecip = 1.0f / ((float)Math.Sqrt(2.0f * MathF.PI) * radius);
            const float radiusModifier = 1.0f;

            for (var i = 0; i < weights.Length; i++) {
                var x = ((-radius + i) * radiusModifier).Pow(2f);
                weights[i] = sqrtTwoPiTimesRadiusRecip * (-x * sqrtTwoPiTimesRadiusRecip).Exp();
            }

            /* NORMALIZE */
            var div = weights.Sum();
            for (var i = 0; i < weights.Length; i++) {
                weights[i] /= div;
            }

            return weights;
        }

        public void Blur(DeviceContextHolder holder, TargetResourceTexture inputOutput, TargetResourceTexture temporary, BaseCamera camera) {
            _effect.FxWeights.Set(_gaussianBlur ?? (_gaussianBlur = GaussianBlur()));
            _effect.FxNearFarValue.Set(new Vector2(camera.NearZValue, camera.FarZValue));

            holder.PrepareQuad(_effect.LayoutPT);

            _effect.FxFirstStepMap.SetResource(inputOutput.View);
            _effect.FxSourcePixel.Set(new Vector2(1f / temporary.Width, 1f / temporary.Height));
            holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
            _effect.TechBlurH.DrawAllPasses(holder.DeviceContext, 6);
            
            _effect.FxFirstStepMap.SetResource(temporary.View);
            _effect.FxSourcePixel.Set(new Vector2(1f / inputOutput.Width, 1f / inputOutput.Height));
            holder.DeviceContext.Rasterizer.SetViewports(inputOutput.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(inputOutput.TargetView);
            _effect.TechBlurV.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}