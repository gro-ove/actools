using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkSslrHelper : IRenderHelper {
        private EffectPpDarkSslr _effect;

        public bool LinearFiltering { get; set; }

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDarkSslr>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView colorMap, ShaderResourceView depth, ShaderResourceView baseReflection, ShaderResourceView normals,
                ICamera camera, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDiffuseMap.SetResource(colorMap);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxBaseReflectionMap.SetResource(baseReflection);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            (LinearFiltering ? _effect.TechSslr_LinearFiltering : _effect.TechSslr).DrawAllPasses(holder.DeviceContext, 6);
        }

        public void FinalStep(DeviceContextHolder holder, ShaderResourceView colorMap, ShaderResourceView firstStep, ShaderResourceView baseReflection, ShaderResourceView normals,
                ICamera camera, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDiffuseMap.SetResource(colorMap);
            _effect.FxFirstStepMap.SetResource(firstStep);
            _effect.FxBaseReflectionMap.SetResource(baseReflection);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            (LinearFiltering ? _effect.TechFinalStep_LinearFiltering : _effect.TechFinalStep).DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}