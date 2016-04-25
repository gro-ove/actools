using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.TargetTextures;
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
        private BlendState _addBlendState;
        private BlendState _transparentBlendState;
        private DepthStencilState _lessEqualDepthState, _greaterReadOnlyDepthState;

        private TargetResourceDepthTexture _gDepthBuffer, _temporaryDepthBuffer;

        private TargetResourceTexture _gBufferBase, _gBufferNormal, _gBufferMaps,
                _temporaryBuffer0, _temporaryBuffer1, _temporaryBuffer2, _temporaryBuffer3,
                _outputBuffer;

        private HdrHelper _hdrHelper;
        private SslrHelper _sslrHelper;
        private ReflectionCubemap _reflectionCubemap;

        protected DirectionalLight Sun;
        private ShadowsDirectional _sunShadows;

        protected override void InitializeInner() {
            _deferredLighting = DeviceContextHolder.GetEffect<EffectDeferredLight>();
            _deferredResult = DeviceContextHolder.GetEffect<EffectDeferredResult>();
            _ppBasic = DeviceContextHolder.GetEffect<EffectPpBasic>();
            _addBlendState = Device.CreateBlendState(BlendOperation.Add);

            _lessEqualDepthState = DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.LessEqual
            });

            _greaterReadOnlyDepthState = DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.Greater
            });

            {
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

                _transparentBlendState = BlendState.FromDescription(Device, desc);
            }

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

            _hdrHelper = DeviceContextHolder.GetHelper<HdrHelper>();
            _sslrHelper = DeviceContextHolder.GetHelper<SslrHelper>();

            _reflectionCubemap = new ReflectionCubemap(1024);
            _reflectionCubemap.Initialize(DeviceContextHolder);

            _sunShadows = new ShadowsDirectional(2048);
            _sunShadows.Initialize(DeviceContextHolder);
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

            _temporaryDepthBuffer.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer0.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer1.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer2.Resize(DeviceContextHolder, Width, Height);
            _temporaryBuffer3.Resize(DeviceContextHolder, Width, Height);
            _outputBuffer.Resize(DeviceContextHolder, Width, Height);

            _hdrHelper.Resize(DeviceContextHolder, Width, Height);
            _sslrHelper.Resize(DeviceContextHolder, Width, Height);
        }

        protected virtual Vector3 GetReflectionCubemapPosition() {
            return Camera.Position;
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();

            var effect = DeviceContextHolder.GetEffect<EffectDeferredGObject>();
            effect.FxAmbientDown.Set(AmbientLower);
            effect.FxAmbientRange.Set(AmbientUpper - AmbientLower);

            if (Sun != null) {
                effect.FxDirectionalLightDirection.Set(Sun.Direction);
                effect.FxLightColor.Set(AmbientLight(Sun.Color));

                if (UseShadows) {
                    _sunShadows.Update(Sun.Direction, Camera.Position);
                    _sunShadows.DrawScene(DeviceContextHolder, this);
                }
            } else {
                effect.FxDirectionalLightDirection.Set(Vector3.Zero);
                effect.FxLightColor.Set(Vector3.Zero);
            }

            if (UseCubemapReflections) {
                _reflectionCubemap.Update(GetReflectionCubemapPosition());
                _reflectionCubemap.DrawScene(DeviceContextHolder, this);
            }

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.StencilView, _gBufferBase.TargetView,
                    _gBufferNormal.TargetView, _gBufferMaps.TargetView);
            DeviceContext.ClearDepthStencilView(_gDepthBuffer.StencilView,
                    DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
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
        public bool UseShadowsFilter = false;
        public bool UseCubemapReflections = true;
        public bool BlurLocalReflections = false;

        protected override void DrawInner() {
            base.DrawInner();

            if (Mode == RenderingMode.DebugLighting) {
                DrawLights(_temporaryBuffer0);
                FinalStep(_temporaryBuffer0, _sunShadows.Splits[0].Buffer);
                return;
            }

            if (Mode == RenderingMode.Result) {
                DeviceContext.ClearDepthStencilView(_temporaryDepthBuffer.StencilView,
                        DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                DeviceContext.OutputMerger.SetTargets(_temporaryDepthBuffer.StencilView);
                DeviceContext.OutputMerger.BlendState = null;

                Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.TransparentMask);
                DrawLights(_temporaryBuffer0, _temporaryDepthBuffer.StencilView);

                if (UseLocalReflections) {
                    DrawReflections(_temporaryBuffer2, BlurLocalReflections ? _temporaryBuffer1 : null, _temporaryBuffer0,
                            _temporaryDepthBuffer.StencilView);
                }

                CombineResult(_temporaryBuffer1, _temporaryBuffer0, UseLocalReflections ? _temporaryBuffer2 : null, null,
                        _temporaryDepthBuffer.StencilView);

                DrawTransparent();
                ProcessHdr(_temporaryBuffer0, _temporaryBuffer2);
            } else {
                DrawLights(_temporaryBuffer0);
                DrawReflections(_temporaryBuffer2, BlurLocalReflections ? _temporaryBuffer1 : null, _temporaryBuffer0);
                CombineResult(_temporaryBuffer1, _temporaryBuffer0, _temporaryBuffer2);
                ProcessHdr(_temporaryBuffer0, _temporaryBuffer1);
            }

            var input = _temporaryBuffer0;
            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                case RenderingMode.DebugPostEffects:
                case RenderingMode.DebugLocalReflections:
                    input = _temporaryBuffer1;
                    break;
            }

            FinalStep(input, UseFxaa);
        }

        public List<ILight> Lights = new List<ILight>();

        private void DrawLights(TargetResourceTexture target, DepthStencilView limitedBy = null) {
            // set blending & prepare quad
            DeviceContext.OutputMerger.BlendState = _addBlendState;
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _deferredLighting.LayoutPT);

            // proper render target
            if (limitedBy == null) {
                DeviceContext.OutputMerger.SetTargets(target.TargetView);
            } else {
                DeviceContext.OutputMerger.SetTargets(limitedBy, target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = _greaterReadOnlyDepthState;
            }

            DeviceContext.ClearRenderTargetView(target.TargetView, ColorTransparent);

            // camera position & matrix
            _deferredLighting.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _deferredLighting.FxEyePosW.Set(Camera.Position);

            // g-buffer
            _deferredLighting.FxBaseMap.SetResource(_gBufferBase.View);
            _deferredLighting.FxNormalMap.SetResource(_gBufferNormal.View);
            _deferredLighting.FxMapsMap.SetResource(_gBufferMaps.View);
            _deferredLighting.FxDepthMap.SetResource(_gDepthBuffer.View);

            // lights!
            if (UseShadows) {
                var depths = _sunShadows.Splits.Take(EffectDeferredLight.NumSplits).Select(x => x.GetShadowDepth(Camera)).ToArray();
                _deferredLighting.FxShadowDepths.Set(new Vector4(depths[0], depths[1], depths[2], depths[3]));
                _deferredLighting.FxShadowMaps.SetResourceArray(_sunShadows.Splits.Take(EffectDeferredLight.NumSplits).Select(x => x.View).ToArray());
                _deferredLighting.FxShadowViewProj.SetMatrixArray(
                        _sunShadows.Splits.Take(EffectDeferredLight.NumSplits).Select(x => x.ShadowTransform).ToArray());
            } else {
                _deferredLighting.FxShadowMaps.SetResource(null);
            }

            Sun.Draw(DeviceContext, _deferredLighting,
                    UseDebugShadows ? SpecialLightMode.Debug : !UseShadows
                            ? SpecialLightMode.Default : UseShadowsFilter ? SpecialLightMode.Shadows : SpecialLightMode.ShadowsWithoutFilter);
            if (limitedBy == null) {
                foreach (var light in Lights) {
                    light.Draw(DeviceContext, _deferredLighting, SpecialLightMode.Default);
                }
            }

            if (limitedBy != null) {
                DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
            }
        }

        private void DrawReflections(TargetResourceTexture target, TargetResourceTexture temporary = null, TargetResourceTexture light = null,
                DepthStencilView limitedBy = null) {
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _sslrHelper.Effect.LayoutPT);
            DeviceContext.OutputMerger.BlendState = null;

            if (limitedBy == null) {
                DeviceContext.OutputMerger.SetTargets(target.TargetView);
            } else {
                DeviceContext.OutputMerger.SetTargets(limitedBy, target.TargetView);
                DeviceContext.OutputMerger.DepthStencilState = _greaterReadOnlyDepthState;
            }

            // camera position & matrix
            _sslrHelper.Effect.FxWorldViewProj.SetMatrix(Camera.ViewProj);
            _sslrHelper.Effect.FxWorldViewProjInv.SetMatrix(Camera.ViewProjInvert);
            _sslrHelper.Effect.FxEyePosW.Set(Camera.Position);

            // g-buffer
            _sslrHelper.Effect.FxBaseMap.SetResource(_gBufferBase.View);
            _sslrHelper.Effect.FxNormalMap.SetResource(_gBufferNormal.View);
            _sslrHelper.Effect.FxDepthMap.SetResource(_gDepthBuffer.View);
            _sslrHelper.Effect.FxLightMap.SetResource(light?.View);

            _sslrHelper.Effect.TechHabrahabrVersion.DrawAllPasses(DeviceContext, 6);

            if (temporary != null) {
                DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionVertically(DeviceContextHolder, target.View, _gBufferMaps.View);

                DeviceContext.OutputMerger.SetTargets(target.TargetView);
                DeviceContextHolder.GetHelper<BlurHelper>().BlurReflectionHorizontally(DeviceContextHolder, temporary.View, _gBufferMaps.View);
            }

            if (limitedBy != null) {
                DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
            }
        }

        public Vector3 AmbientLower, AmbientUpper;

        public float GetBrightness(Vector3 lightColor) {
            return lightColor.X * 0.299f + lightColor.Y * 0.587f + lightColor.Z * 0.114f;
        }

        public Vector3 AmbientLight(Vector3 lightColor) {
            var a = (AmbientLower + AmbientUpper) * 0.5f;
            return new Vector3(lightColor.X * a.X, lightColor.Y * a.Y, lightColor.Z * a.Z);
        }

        public Vector3 FixLight(Vector3 lightColor) {
            var a = (AmbientLower + AmbientUpper) * 0.5f;
            var l = GetBrightness(a);
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
                DeviceContext.OutputMerger.DepthStencilState = _greaterReadOnlyDepthState;
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

            if (limitedBy != null) {
                DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
            }
        }

        private void DrawTransparent() {
            var effect = DeviceContextHolder.GetEffect<EffectDeferredGObject>();
            effect.FxReflectionCubemap.SetResource(_reflectionCubemap.View);
            effect.FxEyePosW.Set(Camera.Position);

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.StencilView, _temporaryBuffer1.TargetView);
            DeviceContext.OutputMerger.BlendState = _transparentBlendState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.TransparentDepth);

            DeviceContext.OutputMerger.DepthStencilState = ReadOnlyDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Transparent);
            DeviceContext.OutputMerger.DepthStencilState = NormalDepthState;
            DeviceContext.OutputMerger.BlendState = null;

            DeviceContext.OutputMerger.SetTargets(_gDepthBuffer.StencilView, _gBufferBase.TargetView,
                    _gBufferNormal.TargetView, _gBufferMaps.TargetView);
            DeviceContext.OutputMerger.DepthStencilState = _lessEqualDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.TransparentDeferred);

            DrawLights(_temporaryBuffer0);

            if (UseLocalReflections) {
                DrawReflections(_temporaryBuffer3, BlurLocalReflections ? _temporaryBuffer2 : null, _temporaryBuffer0);
            }

            CombineResult(_temporaryBuffer2, _temporaryBuffer0, UseLocalReflections ? _temporaryBuffer3 : null, _temporaryBuffer1);
        }

        private void ProcessHdr(TargetResourceTexture target, TargetResourceTexture source) {
            switch (Mode) {
                case RenderingMode.DebugGBuffer:
                case RenderingMode.DebugPostEffects:
                case RenderingMode.DebugLocalReflections:
                    return;
            }

            DeviceContext.OutputMerger.SetTargets(target.TargetView);
            DeviceContext.ClearRenderTargetView(target.TargetView, ColorTransparent);
            DeviceContext.OutputMerger.BlendState = null;
            _hdrHelper.Draw(DeviceContextHolder, source.View);
        }

        ShaderResourceView _areasTexMap, _searchTexMap;

        protected void FinalStep(TargetResourceTexture source, bool fxaa) {
            DeviceContext.ClearRenderTargetView(_outputBuffer.TargetView, ColorTransparent);

            if (fxaa && UseExperimentalSmaa) {
                var effect = DeviceContextHolder.GetEffect<EffectPpSmaa>();
                if (_areasTexMap == null) {
                    _areasTexMap = ShaderResourceView.FromMemory(Device, Resources.AreaTexDX10);
                    _searchTexMap = ShaderResourceView.FromMemory(Device, Resources.SearchTex);
                }

                DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, effect.LayoutPT);
                DeviceContext.OutputMerger.BlendState = null;

                // edges
                DeviceContext.OutputMerger.SetTargets(_gBufferBase.TargetView);
                DeviceContext.ClearRenderTargetView(_gBufferBase.TargetView, ColorTransparent);

                effect.FxScreenSizeSpec.Set(new Vector4(1f / Width, 1f / Height, Width, Height));

                effect.FxInputMap.SetResource(source.View);
                effect.FxDepthMap.SetResource(_gDepthBuffer.View);

                effect.TechSmaa.DrawAllPasses(DeviceContext, 6);

                // b
                DeviceContext.OutputMerger.SetTargets(_temporaryBuffer3.TargetView);
                DeviceContext.ClearRenderTargetView(_temporaryBuffer3.TargetView, ColorTransparent);

                effect.FxEdgesMap.SetResource(_gBufferBase.View);
                effect.FxAreaTexMap.SetResource(_areasTexMap);
                effect.FxSearchTexMap.SetResource(_searchTexMap);

                effect.TechSmaaB.DrawAllPasses(DeviceContext, 6);

                // n
                DeviceContext.OutputMerger.SetTargets(_outputBuffer.TargetView);

                effect.FxBlendMap.SetResource(_temporaryBuffer3.View);

                effect.TechSmaaN.DrawAllPasses(DeviceContext, 6);
            } else if (fxaa && UseExperimentalFxaa) {
                var effect = DeviceContextHolder.GetEffect<EffectPpFxaa311>();
                DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, effect.LayoutPT);

                DeviceContext.OutputMerger.SetTargets(_gBufferMaps.TargetView);
                effect.FxInputMap.SetResource(source.View);
                effect.TechLuma.DrawAllPasses(DeviceContext, 6);

                DeviceContext.OutputMerger.SetTargets(_outputBuffer.TargetView);
                effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
                effect.FxInputMap.SetResource(_gBufferMaps.View);
                effect.TechFxaa.DrawAllPasses(DeviceContext, 6);
            } else {
                DeviceContext.OutputMerger.SetTargets(_outputBuffer.TargetView);
                DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

                _ppBasic.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
                _ppBasic.FxInputMap.SetResource(source.View);
                (fxaa ? _ppBasic.TechFxaa : _ppBasic.TechCopy).DrawAllPasses(DeviceContext, 6);
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
            // drawing GUI
            Sprite.HandleBlendState = false;
            Sprite.HandleDepthStencilState = false;

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer0.TargetView);
            DeviceContext.ClearRenderTargetView(_temporaryBuffer0.TargetView, Color.Transparent);

            DrawSpritesInner();
            Sprite.Flush();

            // blurring
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContextHolder.GetHelper<BlurHelper>()
                               .Blur(DeviceContextHolder, _temporaryBuffer0, _temporaryBuffer1, target: _temporaryBuffer2, iterations: 2);

            // as a shadow
            _ppBasic.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            DeviceContext.OutputMerger.SetTargets(_temporaryBuffer1.TargetView);
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

            _ppBasic.FxInputMap.SetResource(_temporaryBuffer2.View);
            _ppBasic.FxOverlayMap.SetResource(_temporaryBuffer0.View);
            _ppBasic.TechShadow.DrawAllPasses(DeviceContext, 6);

            // combining with _outputBuffer
            ResetTargets();
            DeviceContextHolder.QuadBuffers.Prepare(DeviceContext, _ppBasic.LayoutPT);

            _ppBasic.FxInputMap.SetResource(_outputBuffer.View);
            _ppBasic.FxOverlayMap.SetResource(_temporaryBuffer1.View);
            _ppBasic.TechOverlay.DrawAllPasses(DeviceContext, 6);
        }

        public override void Dispose() {
            base.Dispose();

            _gBufferBase.Dispose();
            _gBufferNormal.Dispose();
            _gBufferMaps.Dispose();
            _gDepthBuffer.Dispose();
            _temporaryBuffer0.Dispose();
            _temporaryBuffer1.Dispose();
            _temporaryBuffer2.Dispose();
            _temporaryBuffer3.Dispose();

            _addBlendState.Dispose();
            _lessEqualDepthState.Dispose();
            _greaterReadOnlyDepthState.Dispose();
            _transparentBlendState.Dispose();
            _reflectionCubemap.Dispose();
        }
    }
}
