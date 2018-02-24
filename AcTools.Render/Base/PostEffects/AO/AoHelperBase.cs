using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public abstract class AoHelperBase  : IRenderHelper {
        // TODO: Move blur to separate effect
        private EffectPpAoBlur _blurEffect;

        public virtual void OnInitialize(DeviceContextHolder holder) {
            _blurEffect = holder.GetEffect<EffectPpAoBlur>();
        }

        public void SetBlurEffectTextures(ShaderResourceView depth, ShaderResourceView normals) {
            _blurEffect.FxDepthMap.SetResource(depth);
            _blurEffect.FxNormalMap.SetResource(normals);
        }

        private bool? _accumulationMode;
        private float _randomSize;

        protected void SetRandomValues(DeviceContextHolder holder, EffectOnlyResourceVariable texture, EffectOnlyVector4Variable size, bool accumulationMode,
                Vector2 targetSize) {
            if (_accumulationMode != accumulationMode) {
                _accumulationMode = accumulationMode;

                var randomSize = accumulationMode ? 16 : 4;
                texture.SetResource(holder.GetRandomTexture(randomSize, randomSize));
                _randomSize = randomSize;

                if (!accumulationMode) {
                    size.Set(new Vector4(targetSize.X / _randomSize, targetSize.Y / _randomSize, 0f, 0f));
                }
            } else if (accumulationMode) {
                size.Set(new Vector4(targetSize.X / _randomSize, targetSize.Y / _randomSize, MathUtils.Random(0f, 1f), MathUtils.Random(0f, 1f)));
            }
        }

        public abstract void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera,
                TargetResourceTexture target, float aoPower, float aoRadiusMultiplier, bool accumulationMode);

        public virtual void OnResize(DeviceContextHolder holder) {}

        private float[] _gaussianBlur;

        private static float[] GaussianBlur() {
            const int radius = EffectPpSsao.BlurRadius;
            var weights = new float[radius * 2 + 1];

            var sqrtTwoPiTimesRadiusRecip = 1f / ((float)Math.Sqrt(2f * MathF.PI) * radius);
            const float radiusModifier = 1f;

            for (var i = 0; i < weights.Length; i++) {
                var x = ((-radius + i) * radiusModifier).Pow(2f);
                weights[i] = sqrtTwoPiTimesRadiusRecip * (-x * sqrtTwoPiTimesRadiusRecip).Exp();
            }

            // Normalize
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