using System.Collections.Generic;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.DeferredShading.Lights;
using AcTools.Render.DeferredShading.PostEffects;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.DeferredShading {
    public abstract class DeferredShadingRenderer : SceneRenderer {
        private static readonly Color4 ColorTransparent = new Color4(0f, 0f, 0f, 0f);

        private EffectDeferredLight _deferredLighting;
        private EffectDeferredResult _deferredResult;
        private EffectPpBasic _ppBasic;
        private BlendState _addBlend;

        private TargetResourceDepthTexture _gDepthBuffer;
        private TargetResourceTexture _gBufferBase, _gBufferNormal, _gBufferMaps, 
            _temporaryBuffer0, _temporaryBuffer1, _temporaryBuffer2;

        private HdrHelper _hdrHelper;
        private SslrHelper _sslrHelper;
        private ReflectionCubemap _reflectionCubemap;

        protected override void InitializeInner() {
            _deferredLighting = DeviceContextHolder.GetEffect<EffectDeferredLight>();
            _deferredResult = DeviceContextHolder.GetEffect<EffectDeferredResult>();
            _ppBasic = DeviceContextHolder.GetEffect<EffectPpBasic>();
            _addBlend = Device.CreateBlendState(BlendOperation.Add);

            _gDepthBuffer = TargetResourceDepthTexture.Create();
            _gBufferBase = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _gBufferNormal = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
            _gBufferMaps = TargetResourceTexture.Create(Format.R16G16B16A16_Float);

            _temporaryBuffer0 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _temporaryBuffer1 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _temporaryBuffer2 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);

            _hdrHelper = DeviceContextHolder.GetHelper<HdrHelper>();
            _sslrHelper = DeviceContextHolder.GetHelper<SslrHelper>();

            _reflectionCubemap = new ReflectionCubemap(1024);
            _reflectionCubemap.Initialize(DeviceContextHolder);
        }

        protected override SampleDescription GetSampleDescription(int msaaQuality) {
            return new SampleDescription(1, 0);
        }

        protected override void ResizeInner() {
            base.ResizeInner();

            _gDepthBuffer.Resize(DeviceContextHolder, Width, Height);
            _gBufferBase.Resize(DeviceContextHolder, Width, Height);
            _gBufferNormal.Resize(DeviceContextHolder, Width, Height);
            _gBufferMaps.Resize(DeviceContextHolder, Width, Height);

            _temporaryBuffer0.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer1.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer2.Resize(DeviceContextHolder, Width, Height);

            _hdrHelper.Resize(DeviceContextHolder, Width, Height);
            _sslrHelper.Resize(DeviceContextHolder, Width, Height);
        }

        protected virtual Vector3 GetReflectionCubemapPosition() {
            return Camera.Position;
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();

            _reflectionCubemap.DrawScene(DeviceContextHolder, GetReflectionCubemapPosition(), this);

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.TargetView, _gBufferBase.TargetView,
                    _gBufferNormal.TargetView, _gBufferMaps.TargetView);
            DeviceContext.ClearDepthStencilView(_gDepthBuffer.TargetView,
                    DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_gBufferBase.TargetView, ColorTransparent);
            DeviceContext.ClearRenderTargetView(_gBufferNormal.TargetView, ColorTransparent);
            DeviceContext.ClearRenderTargetView(_gBufferMaps.TargetView, ColorTransparent);
        }

        public enum RenderingMode {
            DebugGBuffer, DebugPostEffects, DebugLighting, DebugLocalReflections, Result
        }

        public RenderingMode Mode = RenderingMode.Result;

        protected override void DrawInner() {
            base.DrawInner();
            DrawLights();
            DrawReflections();
            CombineResult();

           // if (Mode == RenderingMode.Result) {
                DrawTransparent();
           // }

            ProcessHdr();
            FinalStepWithFxao();
        }

        public List<ILight> Lights = new List<ILight>(); 

        private void DrawLights() {
            // proper render target
            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer0.TargetView);
            DeviceContext.ClearRenderTargetView(_temporaryBuffer0.TargetView, ColorTransparent);

            // set blending & prepare quad
            DeviceContext.OutputMerger.BlendState = _addBlend;
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _deferredLighting.LayoutPT);

            // camera position & matrix
            _deferredLighting.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _deferredLighting.FxEyePosW.Set(Camera.Position);
            
            // g-buffer
            _deferredLighting.FxBaseMap.SetResource(_gBufferBase.View);
            _deferredLighting.FxNormalMap.SetResource(_gBufferNormal.View);
            _deferredLighting.FxMapsMap.SetResource(_gBufferMaps.View);
            _deferredLighting.FxDepthMap.SetResource(_gDepthBuffer.View);
            
            // lights!
            foreach (var light in Lights) {
                light.Draw(DeviceContext, _deferredLighting);
            }
        }

        private void DrawReflections() {
            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer2.TargetView);
            DeviceContext.ClearRenderTargetView(_temporaryBuffer2.TargetView, ColorTransparent);
            DeviceContext.OutputMerger.BlendState = null;

            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _sslrHelper.Effect.LayoutPT);
            
            // camera position & matrix
            _sslrHelper.Effect.FxWorldViewProj.SetMatrix(Camera.ViewProj);
            _sslrHelper.Effect.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _sslrHelper.Effect.FxEyePosW.Set(Camera.Position);
            
            // g-buffer
            _sslrHelper.Effect.FxBaseMap.SetResource(_gBufferBase.View);
            _sslrHelper.Effect.FxNormalMap.SetResource(_gBufferNormal.View);
            _sslrHelper.Effect.FxDepthMap.SetResource(_gDepthBuffer.View);
            _sslrHelper.Effect.FxLightMap.SetResource(_temporaryBuffer0.View);

            _sslrHelper.Effect.TechHabrahabrVersion.DrawAllPasses(DeviceContext, 6);

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer1.TargetView);
            DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionVertically(DeviceContextHolder, _temporaryBuffer2.View,
                    _gBufferMaps.View);

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer2.TargetView);
            DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionHorizontally(DeviceContextHolder, _temporaryBuffer1.View,
                    _gBufferMaps.View);
        }

        public Vector3 AmbientLower, AmbientUpper;

        private void CombineResult() {
            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer1.TargetView);
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _deferredResult.LayoutPT);

            _deferredResult.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _deferredResult.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            _deferredResult.FxAmbientDown.Set(AmbientLower);
            _deferredResult.FxAmbientRange.Set(AmbientUpper - AmbientLower);

            _deferredResult.FxBaseMap.SetResource(_gBufferBase.View);
            _deferredResult.FxNormalMap.SetResource(_gBufferNormal.View);
            _deferredResult.FxMapsMap.SetResource(_gBufferMaps.View);
            _deferredResult.FxDepthMap.SetResource(_gDepthBuffer.View);
            
            _deferredResult.FxEyePosW.Set(Camera.Position);
            _deferredResult.FxLightMap.SetResource(_temporaryBuffer0.View);
            _deferredResult.FxLocalReflectionMap.SetResource(_temporaryBuffer2.View);
            _deferredResult.FxReflectionCubemap.SetResource(_reflectionCubemap.View);

            EffectTechnique tech;
            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                    tech = _deferredResult.TechDebug;
                    break;

                case RenderingMode.DebugPostEffects:
                    tech = _deferredResult.TechDebugPost;
                    break;

                case RenderingMode.DebugLighting:
                    tech = _deferredResult.TechDebugLighting;
                    break;

                case RenderingMode.DebugLocalReflections:
                    tech = _deferredResult.TechDebugLocalReflections;
                    break;

                case RenderingMode.Result:
                    tech = _deferredResult.TechCombine0;
                    break;

                default:
                    return;
            }

            tech.DrawAllPasses(DeviceContext, 6);
        }

        private BlendState _transparentBlendState;

        private BlendState InitializeTransparentBlendState() {
            var desc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            desc.RenderTargets[0].BlendEnable = true;
            desc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
            desc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            desc.RenderTargets[0].BlendOperation = BlendOperation.Add;
            desc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
            desc.RenderTargets[0].DestinationBlendAlpha = BlendOption.One;
            desc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
            desc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

            return BlendState.FromDescription(Device, desc);
        }

        private void DrawTransparent() {
            if (_transparentBlendState == null) {
                _transparentBlendState = InitializeTransparentBlendState();
            }

            var effect = DeviceContextHolder.GetEffect<EffectDeferredGObject>();
            effect.FxReflectionCubemap.SetResource(_reflectionCubemap.View);
            effect.FxEyePosW.Set(Camera.Position);

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.TargetView, _temporaryBuffer1.TargetView);
            DeviceContext.OutputMerger.BlendState = _transparentBlendState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.TransparentDepth);
            
            DeviceContext.OutputMerger.DepthStencilState = ReadOnlyDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Transparent);
            DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
        }

        private void ProcessHdr() {
            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                case RenderingMode.DebugPostEffects:
                case RenderingMode.DebugLocalReflections:
                    return;
            }

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer0.TargetView);
            DeviceContext.ClearRenderTargetView(_temporaryBuffer0.TargetView, ColorTransparent);
            DeviceContext.OutputMerger.BlendState = null;
            _hdrHelper.Draw(DeviceContextHolder, _temporaryBuffer1.View);
        }

        private void FinalStepWithFxao() {
            var input = _temporaryBuffer0;

            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                case RenderingMode.DebugPostEffects:
                case RenderingMode.DebugLocalReflections:
                    input = _temporaryBuffer1;
                    break;
            }

            ResetTargets();
            DeviceContext.ClearRenderTargetView(RenderTargetView, ColorTransparent);
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

            _ppBasic.FxScreenSize.Set(new Vector4(Width, Height, 1f/Width, 1f/Height));
            _ppBasic.FxInputMap.SetResource(input.View);
            _ppBasic.TechFxaa.DrawAllPasses(DeviceContext, 6);
        }

        public override void Dispose() {
            base.Dispose();

            _gBufferBase.Dispose();
            _gBufferNormal.Dispose();
            _gBufferMaps.Dispose();
            _temporaryBuffer0.Dispose();
            _temporaryBuffer1.Dispose();
            _temporaryBuffer2.Dispose();

            _addBlend.Dispose();
            _reflectionCubemap.Dispose();
        }
    }
}
