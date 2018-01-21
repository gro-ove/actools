using System;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public class SsaoHelper : AoHelperBase {
        private EffectPpSsao _effect;

        public override void OnInitialize(DeviceContextHolder holder) {
            base.OnInitialize(holder);

            _effect = holder.GetEffect<EffectPpSsao>();

            var samplesKernel = new Vector4[EffectPpSsao.SampleCount];
            var random = new Random(0);
            for (var i = 0; i < samplesKernel.Length; i++) {
                samplesKernel[i].X = (float)(random.NextDouble() * 2d - 1d);
                samplesKernel[i].Z = (float)(random.NextDouble() * 2d - 1d);
                samplesKernel[i].Y = (float)random.NextDouble();
                samplesKernel[i].Normalize();

                var scale = (float)i / samplesKernel.Length;
                scale = 0.1f.Lerp(1f, scale * scale);
                samplesKernel[i] *= scale;
            }

            _effect.FxSamplesKernel.Set(samplesKernel);
        }

        public override void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera,
                TargetResourceTexture target, float aoPower, float aoRadiusMultiplier, bool accumulationMode) {
            SetBlurEffectTextures(depth, normals);
            SetRandomValues(holder, _effect.FxNoiseMap, _effect.FxNoiseSize, accumulationMode, target.Size);

            holder.DeviceContext.Rasterizer.SetViewports(target.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(target.TargetView);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxNormalMap.SetResource(normals);

            _effect.FxAoPower.Set(aoPower);
            _effect.FxAoRadius.Set(aoRadiusMultiplier * 0.15f);
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSsao.DrawAllPasses(holder.DeviceContext, 6);
        }
    }
}