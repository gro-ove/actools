// #define SSLR_PARAMETRIZED

using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkSslr : IDisposable {
        [NotNull]
        public readonly TargetResourceTexture BufferScene, BufferResult, BufferBaseReflection;

        public DarkSslr() {
            BufferScene = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            BufferResult = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            BufferBaseReflection = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
        }

        public void Dispose() {
            BufferScene.Dispose();
            BufferResult.Dispose();
            BufferBaseReflection.Dispose();
        }

        private EffectPpDarkSslr _effect;
        private BlurHelper _blurHelper;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDarkSslr>();
            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(16, 16));
            _blurHelper = holder.GetHelper<BlurHelper>();
        }

        public void Prepare(DeviceContextHolder holder, bool useMsaa) {
            if (_effect == null) {
                OnInitialize(holder);
            }

            var width = holder.Width;
            var height = holder.Height;
            var sampleDescription = useMsaa ? holder.SampleDescription : (SampleDescription?)null;

            if (BufferScene.Resize(holder, width, height, sampleDescription)) {
                BufferBaseReflection.Resize(holder, width, height, sampleDescription);
                BufferResult.Resize(holder, width, height, null);
            }

            holder.DeviceContext.ClearRenderTargetView(BufferBaseReflection.TargetView, (Color4)new Vector4(0));
        }

        private void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals,
                ICamera camera) {
            holder.DeviceContext.OutputMerger.SetTargets(BufferResult.TargetView);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxBaseReflectionMap.SetResource(BufferBaseReflection.View);
            _effect.FxNormalMap.SetResource(normals);

            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSslr.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void FinalStep(DeviceContextHolder holder, ShaderResourceView colorMap, ShaderResourceView firstStep, ShaderResourceView baseReflection,
                ShaderResourceView normals, ICamera camera, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);

            _effect.FxDiffuseMap.SetResource(colorMap);
            _effect.FxFirstStepMap.SetResource(firstStep);
            _effect.FxBaseReflectionMap.SetResource(baseReflection);
            _effect.FxNormalMap.SetResource(normals);

            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);
            _effect.FxSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));

            _effect.TechFinalStep.DrawAllPasses(holder.DeviceContext, 6);
        }

#if SSLR_PARAMETRIZED
        publuc string GetInformationString() {
            if (SslrAdjustCurrentMode != SslrAdjustMode.None) {
                return $@"Mode: {SslrAdjustCurrentMode}
Start from: {_sslrStartFrom}
Fix multiplier: {_sslrFixMultiplier}
Offset: {_sslrOffset}
Grow fix: {_sslrGrowFix}
Distance threshold: {_sslrDistanceThreshold}";
            }
        }

        public enum SslrAdjustMode {
            None, StartFrom, FixMultiplier, Offset, GrowFix, DistanceThreshold
        }

        public SslrAdjustMode SslrAdjustCurrentMode;
        private bool _sslrParamsChanged = true;

        /*private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultiplier = 0.7f;
        private float _sslrOffset = 0.048f;
        private float _sslrGrowFix = 0.15f;
        private float _sslrDistanceThreshold = 0.092f;*/

        private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultiplier = 0.5f;
        private float _sslrOffset = 0.05f;
        private float _sslrGrowFix = 0.1f;
        private float _sslrDistanceThreshold = 0.01f;

        public void SslrAdjust(float delta) {
            switch (SslrAdjustCurrentMode) {
                case SslrAdjustMode.None:
                    return;
                case SslrAdjustMode.StartFrom:
                    _sslrStartFrom = (_sslrStartFrom + delta / 10f).Clamp(0.0001f, 0.1f);
                    break;
                case SslrAdjustMode.FixMultiplier:
                    _sslrFixMultiplier += delta;
                    break;
                case SslrAdjustMode.Offset:
                    _sslrOffset += delta;
                    break;
                case SslrAdjustMode.GrowFix:
                    _sslrGrowFix += delta;
                    break;
                case SslrAdjustMode.DistanceThreshold:
                    _sslrDistanceThreshold += delta;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _sslrParamsChanged = true;
        }
#endif

        public void Process(DeviceContextHolder holder, ShaderResourceView depthView, ShaderResourceView normalsView, ICamera camera, float blurMultipler,
                TargetResourceTexture blurTemporary, RenderTargetView target) {
            // Prepare SSLR and combine buffers
#if SSLR_PARAMETRIZED
            if (_sslrParamsChanged) {
                _sslrParamsChanged = false;
                var effect = DeviceContextHolder.GetEffect<EffectPpDarkSslr>();
                effect.FxStartFrom.Set(_sslrStartFrom);
                effect.FxFixMultiplier.Set(_sslrFixMultiplier);
                effect.FxOffset.Set(_sslrOffset);
                effect.FxGlowFix.Set(_sslrGrowFix);
                effect.FxDistanceThreshold.Set(_sslrDistanceThreshold);
            }
#endif

            Draw(holder, depthView, normalsView, camera);
            _blurHelper.BlurDarkSslr(holder, BufferResult, blurTemporary, 4f * blurMultipler);
            FinalStep(holder, BufferScene.View, BufferResult.View, BufferBaseReflection.View,
                    normalsView, camera, target);
        }
    }
}