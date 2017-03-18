using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public class SsaoAltHelper : AoHelperBase {
        private EffectPpSsaoAlt _effect;

        public override void OnInitialize(DeviceContextHolder holder) {
            base.OnInitialize(holder);

            _effect = holder.GetEffect<EffectPpSsaoAlt>();
            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(4, 4));

            var samplesKernel = new Vector4[EffectPpSsao.SampleCount];
            for (var i = 0; i < samplesKernel.Length; i++) {
                samplesKernel[i].X = MathUtils.Random(-1f, 1f);
                samplesKernel[i].Z = MathUtils.Random(-1f, 1f);
                samplesKernel[i].Y = MathUtils.Random(0f, 1f);
                samplesKernel[i].Normalize();

                var scale = (float)i / samplesKernel.Length;
                scale = MathUtils.Lerp(0.1f, 1f, scale * scale);
                samplesKernel[i] *= scale;
            }

            _effect.FxSamplesKernel.Set(samplesKernel);
        }

        public override void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target) {
            base.Draw(holder, depth, normals, camera, target);
            _effect.FxNoiseSize.Set(new Vector2(holder.Width / 4f, holder.Height / 4f));

            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxNormalMap.SetResource(normals);
            
            _effect.FxViewProj.SetMatrix(camera.ViewProj);
            _effect.FxViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSsaoVs.DrawAllPasses(holder.DeviceContext, 6);
        }
    }
}