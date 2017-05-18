using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public abstract class AoHelperBase  : IRenderHelper {
        // TODO: move blur to separate effect
        private EffectPpAoBlur _blurEffect;

        public virtual void OnInitialize(DeviceContextHolder holder) {
            _blurEffect = holder.GetEffect<EffectPpAoBlur>();
        }

        public virtual void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target,
                float aoPower) {
            _blurEffect.FxDepthMap.SetResource(depth);
            _blurEffect.FxNormalMap.SetResource(normals);
        }

        public virtual void OnResize(DeviceContextHolder holder) {}

        private float[] _gaussianBlur;

        private static float[] GaussianBlur() {
            var radius = EffectPpSsao.BlurRadius;
            var weights = new float[radius * 2 + 1];

            var sqrtTwoPiTimesRadiusRecip = 1f / ((float)Math.Sqrt(2f * MathF.PI) * radius);
            const float radiusModifier = 1f;

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

        // Blur is usually the same for all types of AO
        public void Blur(DeviceContextHolder holder, TargetResourceTexture inputOutput, TargetResourceTexture temporary, CameraBase camera) {
            _blurEffect.FxWeights.Set(_gaussianBlur ?? (_gaussianBlur = GaussianBlur()));
            _blurEffect.FxNearFarValue.Set(new Vector2(camera.NearZValue, camera.FarZValue));

            holder.PrepareQuad(_blurEffect.LayoutPT);

            _blurEffect.FxFirstStepMap.SetResource(inputOutput.View);
            _blurEffect.FxSourcePixel.Set(new Vector2(1f / temporary.Width, 1f / temporary.Height));
            holder.DeviceContext.Rasterizer.SetViewports(temporary.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
            _blurEffect.TechBlurH.DrawAllPasses(holder.DeviceContext, 6);

            _blurEffect.FxFirstStepMap.SetResource(temporary.View);
            _blurEffect.FxSourcePixel.Set(new Vector2(1f / inputOutput.Width, 1f / inputOutput.Height));
            holder.DeviceContext.Rasterizer.SetViewports(inputOutput.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(inputOutput.TargetView);
            _blurEffect.TechBlurV.DrawAllPasses(holder.DeviceContext, 6);
        }

        public virtual void Dispose() { }
    }
}