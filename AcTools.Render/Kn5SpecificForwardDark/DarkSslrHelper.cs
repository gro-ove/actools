using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkSslrHelper : IRenderHelper {
        private EffectPpDarkSslr _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDarkSslr>();
            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(16, 16));
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView baseReflection, ShaderResourceView normals,
                ICamera camera, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxBaseReflectionMap.SetResource(baseReflection);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSslr.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void FinalStep(DeviceContextHolder holder, ShaderResourceView colorMap, ShaderResourceView firstStep, ShaderResourceView baseReflection,
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
            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));

            _effect.TechFinalStep.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}