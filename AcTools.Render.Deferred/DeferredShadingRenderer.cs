using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Deferred.Lights;
using AcTools.Render.Deferred.PostEffects;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Deferred {
    public abstract class DeferredShadingRenderer : SceneRenderer {
        private EffectDeferredLight _deferredLighting;
        private EffectDeferredResult _deferredResult;
        private EffectPpBasic _ppBasic;

        private TargetResourceDepthTexture _gDepthBuffer, _temporaryDepthBuffer;

        private TargetResourceTexture _gBufferBase, _gBufferNormal, _gBufferMaps,
                _temporaryBuffer0, _temporaryBuffer1, _temporaryBuffer2, _temporaryBuffer3,
                _outputBuffer;
        
        private ReflectionCubemap _reflectionCubemap;
        protected virtual Vector3 ReflectionCubemapPosition => Camera.Position;

        [CanBeNull]
        public DirectionalLight Sun { get; protected set; }
        private ShadowsDirectional _sunShadows;

        protected override void InitializeInner() {
            _deferredLighting = DeviceContextHolder.GetEffect<EffectDeferredLight>();
            _deferredResult = DeviceContextHolder.GetEffect<EffectDeferredResult>();
            _ppBasic = DeviceContextHolder.GetEffect<EffectPpBasic>();

            _gDepthBuffer = TargetResourceDepthTexture.Create();
            _gBufferBase = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _gBufferNormal = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
            _gBufferMaps = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _temporaryDepthBuffer = TargetResourceDepthTexture.Create();
            _temporaryBuffer0 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _temporaryBuffer1 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _temporaryBuffer2 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _temporaryBuffer3 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _outputBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _reflectionCubemap = new ReflectionCubemap(1024);
            _reflectionCubemap.Initialize(DeviceContextHolder);

            _sunShadows = new ShadowsDirectional(2048);
            _sunShadows.Initialize(DeviceContextHolder);
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_11_0;

        protected override void ResizeInner() {
            base.ResizeInner();

            _gDepthBuffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _gBufferBase.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _gBufferNormal.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _gBufferMaps.Resize(DeviceContextHolder, Width, Height, SampleDescription);

            _temporaryDepthBuffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _temporaryBuffer0.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _temporaryBuffer1.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _temporaryBuffer2.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _temporaryBuffer3.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _outputBuffer.Resize(DeviceContextHolder, Width, Height, SampleDescription);
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();

            if (Sun != null && UseShadows) {
                _sunShadows.Update(Sun.Direction, Camera);
                _sunShadows.DrawScene(DeviceContextHolder, this);
            }

            if (UseCubemapReflections) {
                _reflectionCubemap.Update(ReflectionCubemapPosition);
                _reflectionCubemap.DrawScene(DeviceContextHolder, this);
            }

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.DepthView, _gBufferBase.TargetView,
                    _gBufferNormal.TargetView, _gBufferMaps.TargetView);
            DeviceContext.ClearDepthStencilView(_gDepthBuffer.DepthView,
                    DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.ClearRenderTargetView(_gBufferBase.TargetView, ColorTransparent);
            DeviceContext.ClearRenderTargetView(_gBufferNormal.TargetView, ColorTransparent);
            DeviceContext.ClearRenderTargetView(_gBufferMaps.TargetView, ColorTransparent);
            DeviceContext.OutputMerger.BlendState = null;
        }

        public enum RenderingMode {
            DebugGBuffer, DebugPostEffects, DebugLighting, DebugLocalReflections,
            Result, WithoutTransparent
        }

        public RenderingMode Mode = RenderingMode.Result;
        public bool UseFxaa = true;
        public bool UseExperimentalFxaa = false;
        public bool UseExperimentalSmaa = false;
        public bool UseLocalReflections = true;
        public bool UseShadows = true;
        public bool UseDebugShadows = false;
        public bool UseShadowsFilter = true;
        public bool UseCubemapReflections = true;
        public bool BlurLocalReflections = false;
        public bool LimitLightsThroughGlass = true;

        protected override void DrawInner() {
            base.DrawInner();

            switch (Mode) {
                case RenderingMode.Result:
                    DeviceContext.ClearDepthStencilView(_temporaryDepthBuffer.DepthView,
                            DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                    DeviceContext.OutputMerger.SetTargets(_temporaryDepthBuffer.DepthView);
                    DeviceContext.OutputMerger.BlendState = null;

                    Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.DeferredTransparentMask);
                    DrawLights(_temporaryBuffer0, _temporaryDepthBuffer.DepthView);

                    if (UseLocalReflections) {
                        DrawReflections(_temporaryBuffer2, BlurLocalReflections ? _temporaryBuffer1 : null, _temporaryBuffer0,
                                _temporaryDepthBuffer.DepthView);
                    }

                    CombineResult(_temporaryBuffer1, _temporaryBuffer0, UseLocalReflections ? _temporaryBuffer2 : null, null,
                            _temporaryDepthBuffer.DepthView);

                    DrawTransparent();
                    ProcessHdr(_temporaryBuffer0, _temporaryBuffer2, _temporaryBuffer3);
                    FinalStep(_temporaryBuffer0);
                    break;

                case RenderingMode.WithoutTransparent:
                    DrawLights(_temporaryBuffer0);
                    DrawReflections(_temporaryBuffer2, BlurLocalReflections ? _temporaryBuffer1 : null, _temporaryBuffer0);
                    CombineResult(_temporaryBuffer1, _temporaryBuffer0, _temporaryBuffer2);
                    ProcessHdr(_temporaryBuffer0, _temporaryBuffer1, _temporaryBuffer3);
                    FinalStep(_temporaryBuffer0);
                    break;

                case RenderingMode.DebugGBuffer:
                    CombineResult(_temporaryBuffer0, null, null);
                    FinalStep(_temporaryBuffer0);
                    return;

                case RenderingMode.DebugLighting:
                    DrawLights(_temporaryBuffer0);
                    FinalStep(_temporaryBuffer0, _sunShadows.Splits[0].Buffer);
                    break;

                case RenderingMode.DebugLocalReflections:
                case RenderingMode.DebugPostEffects:
                    DrawLights(_temporaryBuffer0);
                    DrawReflections(_temporaryBuffer2, BlurLocalReflections ? _temporaryBuffer1 : null, _temporaryBuffer0);
                    CombineResult(_temporaryBuffer1, _temporaryBuffer0, _temporaryBuffer2);
                    FinalStep(_temporaryBuffer1);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public List<ILight> Lights = new List<ILight>();

        private void DrawLights(TargetResourceTexture target, DepthStencilView limitedBy = null) {
            // set blending & prepare quad
            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddBlendState;

            // proper render target
            if (limitedBy == null) {
                DeviceContext.OutputMerger.SetTargets(target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = null;
            } else {
                DeviceContext.OutputMerger.SetTargets(limitedBy, target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.GreaterReadOnlyDepthState;
            }

            DeviceContext.ClearRenderTargetView(target.TargetView, ColorTransparent);

            // camera position & matrix
            _deferredLighting.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _deferredLighting.FxEyePosW.Set(Camera.Position);
            _deferredLighting.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            // g-buffer
            _deferredLighting.FxBaseMap.SetResource(_gBufferBase.View);
            _deferredLighting.FxNormalMap.SetResource(_gBufferNormal.View);
            _deferredLighting.FxMapsMap.SetResource(_gBufferMaps.View);
            _deferredLighting.FxDepthMap.SetResource(_gDepthBuffer.View);

            // lights!
            if (UseShadows) {
                _deferredLighting.FxShadowMaps.SetResourceArray(_sunShadows.Splits.Take(EffectDeferredLight.NumSplits).Select(x => x.View).ToArray());
                _deferredLighting.FxShadowViewProj.SetMatrixArray(
                        _sunShadows.Splits.Take(EffectDeferredLight.NumSplits).Select(x => x.ShadowTransform).ToArray());
            } else {
                _deferredLighting.FxShadowMaps.SetResource(null);
            }

            Sun?.Draw(DeviceContextHolder, Camera,
                    UseDebugShadows ? SpecialLightMode.Debug : !UseShadows ? SpecialLightMode.Default :
                            UseShadowsFilter ? SpecialLightMode.Shadows : SpecialLightMode.ShadowsWithoutFilter);
            if (!LimitLightsThroughGlass || limitedBy == null) {
                foreach (var light in Lights) {
                    light.Draw(DeviceContextHolder, Camera, SpecialLightMode.Default);
                }
            }

            if (limitedBy != null) {
                DeviceContext.OutputMerger.DepthStencilState = null;
            }
        }

        private void DrawReflections(TargetResourceTexture target, TargetResourceTexture temporary = null, TargetResourceTexture light = null, DepthStencilView limitedBy = null) {
            var sslr = DeviceContextHolder.GetHelper<SslrHelper>();

            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, sslr.Effect.LayoutPT);
            DeviceContext.OutputMerger.BlendState = null;

            if (limitedBy == null) DeviceContext.OutputMerger.SetTargets(target.TargetView);
            else {
                DeviceContext.OutputMerger.SetTargets(limitedBy, target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.GreaterReadOnlyDepthState;
            }

            // camera position & matrix
            sslr.Effect.FxWorldViewProj.SetMatrix(Camera.ViewProj);
            sslr.Effect.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            sslr.Effect.FxEyePosW.Set(Camera.Position);

            // g-buffer
            sslr.Effect.FxBaseMap.SetResource(_gBufferBase.View);
            sslr.Effect.FxNormalMap.SetResource(_gBufferNormal.View);
            sslr.Effect.FxDepthMap.SetResource(_gDepthBuffer.View);
            sslr.Effect.FxLightMap.SetResource(light?.View);

            sslr.Effect.TechHabrahabrVersion.DrawAllPasses(DeviceContext, 6);

            if (temporary != null) {
                DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionVertically(DeviceContextHolder, target.View, _gBufferMaps.View);

                DeviceContext.OutputMerger.SetTargets(target.TargetView);
                DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionHorizontally(DeviceContextHolder, temporary.View, _gBufferMaps.View);
            }

            if (limitedBy != null) {
                DeviceContext.OutputMerger.DepthStencilState = null;
            }
        }

        public Vector3 AmbientLower, AmbientUpper;

        public Vector3 AmbientLight(Vector3 lightColor) {
            var a = (AmbientLower + AmbientUpper) * 0.5f;
            return new Vector3(lightColor.X * a.X, lightColor.Y * a.Y, lightColor.Z * a.Z);
        }

        public Vector3 FixLight(Vector3 lightColor) {
            var a = (AmbientLower + AmbientUpper) * 0.5f;
            var l = a.GetBrightness();
            return new Vector3(lightColor.X * l / a.X, lightColor.Y * l / a.Y, lightColor.Z * l / a.Z);
        }

        private void CombineResult(TargetResourceTexture target, TargetResourceTexture light, TargetResourceTexture reflection,
                TargetResourceTexture bottomLayer = null, DepthStencilView limitedBy = null) {
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _deferredResult.LayoutPT);
            DeviceContext.OutputMerger.BlendState = null;

            if (limitedBy == null) {
                DeviceContext.OutputMerger.SetTargets(target.TargetView);
            } else {
                DeviceContext.OutputMerger.SetTargets(limitedBy, target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.GreaterReadOnlyDepthState;
            }

            DeviceContext.ClearRenderTargetView(target.TargetView, ColorTransparent);

            _deferredResult.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _deferredResult.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            _deferredResult.FxBaseMap.SetResource(_gBufferBase.View);
            _deferredResult.FxNormalMap.SetResource(_gBufferNormal.View);
            _deferredResult.FxMapsMap.SetResource(_gBufferMaps.View);
            _deferredResult.FxDepthMap.SetResource(_gDepthBuffer.View);

            _deferredResult.FxEyePosW.Set(Camera.Position);
            _deferredResult.FxLightMap.SetResource(light?.View);
            _deferredResult.FxLocalReflectionMap.SetResource(reflection?.View);
            _deferredResult.FxBottomLayerMap.SetResource(bottomLayer?.View);
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
                case RenderingMode.WithoutTransparent:
                    tech = _deferredResult.TechCombine0;
                    break;

                default:
                    return;
            }

            tech.DrawAllPasses(DeviceContext, 6);

            if (limitedBy != null) DeviceContext.OutputMerger.DepthStencilState = null;
        }

        private void DrawTransparent() {
            var effect = DeviceContextHolder.GetEffect<EffectDeferredGObject>();
            effect.FxReflectionCubemap.SetResource(_reflectionCubemap.View);
            effect.FxEyePosW.Set(Camera.Position);
            
            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.DepthView, _temporaryBuffer1.TargetView);
            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.TransparentBlendState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.DeferredTransparentDepth);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.DeferredTransparentForw);
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.DepthView, _gBufferBase.TargetView, _gBufferNormal.TargetView, _gBufferMaps.TargetView);
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.DeferredTransparentDef);

            DrawLights(_temporaryBuffer0);
            if (UseLocalReflections) DrawReflections(_temporaryBuffer3, BlurLocalReflections ? _temporaryBuffer2 : null, _temporaryBuffer0);

            CombineResult(_temporaryBuffer2, _temporaryBuffer0, UseLocalReflections ? _temporaryBuffer3 : null, _temporaryBuffer1);
        }

        private void ProcessHdr(TargetResourceTexture target, TargetResourceTexture source, TargetResourceTexture temporary) {
            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                case RenderingMode.DebugPostEffects:
                case RenderingMode.DebugLocalReflections:
                    return;
            }

            DeviceContext.OutputMerger.SetTargets(target.TargetView);
            DeviceContext.ClearRenderTargetView(target.TargetView, ColorTransparent);
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContextHolder.GetHelper<HdrHelper>().Draw(DeviceContextHolder, source.View, temporary);
        }

        protected void FinalStep(TargetResourceTexture source) {
            DeviceContext.ClearRenderTargetView(_outputBuffer.TargetView, ColorTransparent);

            if (!UseFxaa) {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, source.View, _outputBuffer.TargetView);
                return;
            }

            if (UseExperimentalSmaa) {
                DeviceContextHolder.GetHelper<SmaaHelper>().Draw(DeviceContextHolder, source.View, _outputBuffer.TargetView, _gBufferBase, _temporaryBuffer3);
            } else if (UseExperimentalFxaa) {
                DeviceContextHolder.GetHelper<Fxaa311Helper>().Draw(DeviceContextHolder, source.View, _outputBuffer.TargetView, _gBufferMaps);
            } else {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, source.View, _outputBuffer.TargetView);
            }
        }

        protected void FinalStep(TargetResourceTexture source, TargetResourceDepthTexture depth, float sizeMultipler = 0.25f) {
            DeviceContext.OutputMerger.SetTargets(_outputBuffer.TargetView);
            DeviceContext.ClearRenderTargetView(_outputBuffer.TargetView, ColorTransparent);
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

            _ppBasic.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
            _ppBasic.FxSizeMultipler.Set(sizeMultipler);
            _ppBasic.FxInputMap.SetResource(source.View);
            _ppBasic.FxDepthMap.SetResource(depth.View);
            _ppBasic.TechDepth.DrawAllPasses(DeviceContext, 6);
        }

        protected virtual void DrawSpritesInner() {}

        protected sealed override void DrawSprites() {
            if (Sprite == null) throw new NotSupportedException();

            // drawing GUI
            Sprite.HandleBlendState = false;
            Sprite.HandleDepthStencilState = false;

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer0.TargetView);
            DeviceContext.ClearRenderTargetView(_temporaryBuffer0.TargetView, Color.Transparent);

            DrawSpritesInner();
            Sprite.Flush();

            // blurring
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContextHolder.GetHelper<BlurHelper>().Blur(DeviceContextHolder, _temporaryBuffer0, _temporaryBuffer1, target: _temporaryBuffer2, iterations: 2);

            // as a shadow
            _ppBasic.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer1.TargetView);
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

            _ppBasic.FxInputMap.SetResource(_temporaryBuffer2.View);
            _ppBasic.FxOverlayMap.SetResource(_temporaryBuffer0.View);
            _ppBasic.TechShadow.DrawAllPasses(DeviceContext, 6);

            // combining with _outputBuffer
            ResetTargets();
            DeviceContextHolder.PrepareQuad(_ppBasic.LayoutPT);

            _ppBasic.FxInputMap.SetResource(_outputBuffer.View);
            _ppBasic.FxOverlayMap.SetResource(_temporaryBuffer1.View);
            _ppBasic.TechOverlay.DrawAllPasses(DeviceContext, 6);
        }

        public bool KeepFxaaWhileShooting;
        
        public override void Shot(double multipler, double downscale, Stream outputStream, bool lossless) {
            if (KeepFxaaWhileShooting || Equals(multipler, 1d) && Equals(downscale, 1d)) {
                base.Shot(multipler, downscale, outputStream, lossless);
            } else {
                var useFxaa = UseFxaa;
                UseFxaa = false;

                try {
                    base.Shot(multipler, downscale, outputStream, lossless);
                } finally {
                    UseFxaa = useFxaa;
                }
            }
        }

        public override void Dispose() {
            base.Dispose();

            Lights.DisposeEverything();

            DisposeHelper.Dispose(ref _gBufferBase);
            DisposeHelper.Dispose(ref _gBufferNormal);
            DisposeHelper.Dispose(ref _gBufferMaps);
            DisposeHelper.Dispose(ref _gDepthBuffer);

            DisposeHelper.Dispose(ref _temporaryDepthBuffer);
            DisposeHelper.Dispose(ref _temporaryBuffer0);
            DisposeHelper.Dispose(ref _temporaryBuffer1);
            DisposeHelper.Dispose(ref _temporaryBuffer2);
            DisposeHelper.Dispose(ref _temporaryBuffer3);

            DisposeHelper.Dispose(ref _reflectionCubemap);
            DisposeHelper.Dispose(ref _sunShadows);
        }
    }
}
