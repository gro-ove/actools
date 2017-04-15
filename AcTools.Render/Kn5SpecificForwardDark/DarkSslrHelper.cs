using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkSslrHelper : IRenderHelper {
        private EffectPpDarkSslr _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDarkSslr>();
            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(16, 16));

            /*_depthDown16 = TargetResourceTexture.Create(Format.R32_Float);
            _depthDown4 = TargetResourceTexture.Create(Format.R32_Float);
            _depthDown256 = TargetResourceTexture.Create(Format.R32_Float);
            _depthDown64 = TargetResourceTexture.Create(Format.R32_Float);*/
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

        /*private TargetResourceTexture _depthDown16, _depthDown256;
        private TargetResourceTexture _depthDown4, _depthDown64;

        private void Downscale4(DeviceContextHolder holder, TargetResourceTexture target, ShaderResourceView baseDepth, int baseWidth, int baseSize) {
            target.Resize(holder, baseWidth / 4, baseSize / 4, null);
            _effect.FxDepthMap.SetResource(baseDepth);
            holder.DeviceContext.Rasterizer.SetViewports(target.Viewport);
            holder.DeviceContext.OutputMerger.SetTargets(target.TargetView);
            holder.DeviceContext.ClearRenderTargetView(target.TargetView, default(Color4));
            _effect.FxSize.Set(target.FxSize);
            _effect.TechDownscale4.DrawAllPasses(holder.DeviceContext, 6);
        }

        private void Downscale4(DeviceContextHolder holder, TargetResourceTexture target, TargetResourceTexture baseTex) {
            Downscale4(holder, target, baseTex.View, baseTex.Width, baseTex.Height);
        }*/
        
        public void DrawExt(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView baseReflection, ShaderResourceView normals,
                ICamera camera, RenderTargetView target) {
            throw new NotSupportedException();

            /*holder.PrepareQuad(_effect.LayoutPT);
            holder.SaveRenderTargetAndViewport();

            // prepare depth-down
            Downscale4(holder, _depthDown4, depth, holder.Width, holder.Height);
            Downscale4(holder, _depthDown16, _depthDown4);
            Downscale4(holder, _depthDown64, _depthDown16);
            Downscale4(holder, _depthDown256, _depthDown64);

            // sslr
            holder.RestoreRenderTargetAndViewport();
            holder.DeviceContext.OutputMerger.SetTargets(target);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxDepthMapDown.SetResource(_depthDown4.View);
            _effect.FxDepthMapDownMore.SetResource(_depthDown16.View);
            _effect.FxBaseReflectionMap.SetResource(baseReflection);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxEyePosW.Set(camera.Position);
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);

            _effect.TechSslrExt.DrawAllPasses(holder.DeviceContext, 6);*/
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
            _effect.FxSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));

            _effect.TechFinalStep.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }
}