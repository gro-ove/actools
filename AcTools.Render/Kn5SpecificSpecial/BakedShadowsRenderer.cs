using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class BakedShadowsRenderer : ShadowsRendererBase {
        public BakedShadowsRenderer([NotNull] Kn5 kn5, [CanBeNull] DataWrapper carData) : base(kn5, carData) {
            ResolutionMultiplier = 2d;
        }

        public float SkyBrightnessLevel = 2.5f;
        public float ΘFrom = -10.0f;
        public float ΘTo = 50.0f;
        public float Gamma = 0.5f;
        public float Ambient = 0.3f;
        public float ShadowBias = 0.0f;
        public int Iterations = 500;
        public bool DebugMode = false;

        private RasterizerState _rasterizerState;

        private void InitializeBuffers() {
            _shadowBuffer = TargetResourceDepthTexture.Create();
            _bufferFSumm = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
            _bufferF1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _bufferF2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _bufferA = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _summBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = BlendOption.One,
                DestinationBlendAlpha = BlendOption.One,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            });

            _bakedBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Maximum,
                SourceBlendAlpha = BlendOption.One,
                DestinationBlendAlpha = BlendOption.One,
                BlendOperationAlpha = BlendOperation.Maximum,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            });

            _effect = DeviceContextHolder.GetEffect<EffectSpecialShadow>();

            _rasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = (int)(100 * ShadowBias),
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = ShadowBias
            });
        }

        protected override void InitializeInner() {
            base.InitializeInner();
            InitializeBuffers();
            DeviceContextHolder.Set<INormalsNormalTexturesProvider>(new NormalsNormalsTexturesProvider(Kn5));
        }

        private void PrepareBuffers(int shadowResolution) {
            Resize();

            _shadowBuffer.Resize(DeviceContextHolder, shadowResolution, shadowResolution, null);
            _shadowViewport = new Viewport(0, 0, _shadowBuffer.Width, _shadowBuffer.Height, 0, 1.0f);

            _bufferFSumm.Resize(DeviceContextHolder, Width, Height, null);
            _bufferF1.Resize(DeviceContextHolder, Width, Height, null);
            _bufferF2.Resize(DeviceContextHolder, Width, Height, null);
            _bufferA.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            DeviceContext.ClearRenderTargetView(_bufferFSumm.TargetView, new Color4(0f, 0f, 0f, 0f));
        }

        private Viewport _shadowViewport;
        private TargetResourceDepthTexture _shadowBuffer;
        private CameraOrtho _shadowCamera;
        private TargetResourceTexture _bufferFSumm, _bufferF1, _bufferF2, _bufferA;
        private BlendState _summBlendState, _bakedBlendState;
        private EffectSpecialShadow _effect;

        private Kn5RenderableDepthOnlyObject[] _flattenNodes;
        private Kn5RenderableDepthOnlyObject[] _filteredNodes;

        private void DrawShadow(Vector3 from, Vector3? up = null) {
            from.Normalize();
            _effect.FxLightDir.Set(from);

            DeviceContext.Rasterizer.State = _rasterizerState;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.SetViewports(_shadowViewport);

            DeviceContext.ClearDepthStencilView(_shadowBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(_shadowBuffer.DepthView);

            _shadowCamera.LookAt(from * _shadowCamera.FarZValue * 0.5f, Vector3.Zero, up ?? Vector3.UnitY);
            _shadowCamera.UpdateViewMatrix();

            if (_flattenNodes == null) {
                _flattenNodes = Flatten(Scene, x => (x as Kn5RenderableDepthOnlyObject)?.OriginalNode.CastShadows != false &&
                        IsVisible(x)).OfType<Kn5RenderableDepthOnlyObject>().ToArray();
            }

            for (var i = 0; i < _flattenNodes.Length; i++) {
                _flattenNodes[i].Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Simple);
            }
        }

        private void AddShadow() {
            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.BlendState = _bakedBlendState;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);

            DeviceContext.OutputMerger.SetTargets(_bufferF1.TargetView);
            DeviceContext.ClearRenderTargetView(_bufferF1.TargetView, new Color4(0f, 0f, 0f, 0f));
            _effect.FxDepthMap.SetResource(_shadowBuffer.View);
            _effect.FxShadowViewProj.SetMatrix(_shadowCamera.ViewProj * new Matrix {
                M11 = 0.5f,
                M22 = -0.5f,
                M33 = 1.0f,
                M41 = 0.5f,
                M42 = 0.5f,
                M44 = 1.0f
            });

            for (var i = 0; i < _filteredNodes.Length; i++) {
                _filteredNodes[i].Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Shadow);
            }

            // copy to summary buffer
            DeviceContext.OutputMerger.BlendState = _summBlendState;
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _bufferF1.View, _bufferFSumm.TargetView);
        }

        private void Draw(float multipler, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            _effect.FxAmbient.Set(Ambient);
            DeviceContext.ClearRenderTargetView(_bufferFSumm.TargetView, Color.Transparent);
            DeviceContext.ClearRenderTargetView(_bufferA.TargetView, Color.Transparent);

            var t = Iterations;

            // draw
            var iter = 0f;
            var sw = Stopwatch.StartNew();
            for (var k = 0; k < t; k++) {
                if (sw.ElapsedMilliseconds > 20) {
                    if (cancellation.IsCancellationRequested) return;
                    progress?.Report(0.2 + 0.8 * k / t);
                    sw.Restart();
                }

                if (DebugMode) {
                    DrawShadow(Vector3.UnitY, Vector3.UnitZ);
                } else {
                    var v3 = default(Vector3);
                    var vn = default(Vector3);
                    var length = 0f;

                    var yFrom = (90f - ΘFrom).ToRadians().Cos();
                    var yTo = (90f - ΘTo).ToRadians().Cos();

                    if (yTo < yFrom) {
                        throw new Exception("yTo < yFrom");
                    }

                    do {
                        var x = MathF.Random(-1f, 1f);
                        var y = MathF.Random(yFrom < 0f ? -1f : 0f, yTo > 0f ? 1f : 0f);
                        var z = MathF.Random(-1f, 1f);
                        if (x.Abs() < 0.01 && z.Abs() < 0.01) continue;

                        v3 = new Vector3(x, y, z);
                        length = v3.Length();
                        vn = v3 / length;
                    } while (length > 1f || vn.Y < yFrom || vn.Y > yTo);

                    DrawShadow(v3);
                }

                AddShadow();
                iter++;
            }

            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);
            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);
            _effect.FxSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            DeviceContext.ClearRenderTargetView(_bufferF1.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(_bufferF1.TargetView);
            _effect.FxInputMap.SetResource(_bufferFSumm.View);
            _effect.FxCount.Set(iter / SkyBrightnessLevel);
            _effect.FxMultipler.Set(multipler);
            _effect.FxGamma.Set(Gamma);
            _effect.TechAoResult.DrawAllPasses(DeviceContext, 6);

            for (var i = 0; i < 2; i++) {
                DeviceContext.ClearRenderTargetView(_bufferF2.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_bufferF2.TargetView);
                _effect.FxInputMap.SetResource(_bufferF1.View);
                _effect.TechAoGrow.DrawAllPasses(DeviceContext, 6);

                DeviceContext.ClearRenderTargetView(_bufferF1.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_bufferF1.TargetView);
                _effect.FxInputMap.SetResource(_bufferF2.View);
                _effect.TechAoGrow.DrawAllPasses(DeviceContext, 6);
            }

            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _bufferF1.View, _bufferF2.TargetView);

                PrepareForFinalPass();
                DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, _bufferF2, _bufferA);
            } else {
                PrepareForFinalPass();
                DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, _bufferF1, _bufferA);
            }

            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _bufferA.View, RenderTargetView);

            /*DeviceContext.OutputMerger.SetTargets(RenderTargetView);
            _effect.FxInputMap.SetResource(_bufferA.View);
            _effect.TechAoFinalCopy.DrawAllPasses(DeviceContext, 6);*/
        }

        private void SetBodyShadowCamera() {
            var size = CarNode.BoundingBox?.GetSize().Length() ?? 4f;
            _shadowCamera = new CameraOrtho {
                Width = size,
                Height = size,
                NearZ = 1f,
                FarZ = 20f,
                DisableFrustum = true
            };
            _shadowCamera.SetLens(1f);
        }

        public bool UseFxaa = false;

        public void Shot(string outputFile, string textureName, [CanBeNull] string objectPath, [CanBeNull] IProgress<double> progress,
                CancellationToken cancellation) {
            if (!Initialized) {
                Initialize();
                if (cancellation.IsCancellationRequested) return;
            }

            _filteredNodes = Flatten(Kn5, Scene, textureName, objectPath).OfType<Kn5RenderableDepthOnlyObject>().ToArray();
            PrepareBuffers(2048);
            SetBodyShadowCamera();
            if (cancellation.IsCancellationRequested) return;

            Draw(1f, progress, cancellation);
            if (cancellation.IsCancellationRequested) return;

            Texture2D.ToFile(DeviceContext, RenderBuffer, ImageFileFormat.Png, outputFile);
        }

        protected override void OnTickOverride(float dt) { }

        private class NormalsNormalsTexturesProvider : INormalsNormalTexturesProvider {
            private readonly Kn5 _kn5;

            public NormalsNormalsTexturesProvider(Kn5 kn5) {
                _kn5 = kn5;
            }

            private Kn5TexturesProvider _texturesProvider;
            private readonly Dictionary<uint, Tuple<IRenderableTexture, float>[]> _cache = new Dictionary<uint, Tuple<IRenderableTexture, float>[]>();

            Tuple<IRenderableTexture, float> INormalsNormalTexturesProvider.GetTexture(IDeviceContextHolder contextHolder, uint materialId) {
                Tuple<IRenderableTexture, float>[] result;
                if (!_cache.TryGetValue(materialId, out result)) {
                    if (_texturesProvider == null) {
                        _texturesProvider = new Kn5TexturesProvider(_kn5, false);
                    }

                    var material = _kn5.GetMaterial(materialId);
                    var textureName = material?.GetMappingByName("txNormal")?.Texture;
                    if (textureName != null && !material.ShaderName.Contains("damage")) {
                        var texture = _texturesProvider.GetTexture(contextHolder, textureName);
                        result = new[] { Tuple.Create(texture, material.GetPropertyValueAByName("normalMult") + 1f) };
                    } else {
                        result = new Tuple<IRenderableTexture, float>[] { null };
                    }

                    _cache[materialId] = result;
                }

                return result[0];
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _texturesProvider);
                _cache.Clear();
            }
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _summBlendState);
            DisposeHelper.Dispose(ref _bakedBlendState);
            DisposeHelper.Dispose(ref _bufferFSumm);
            DisposeHelper.Dispose(ref _bufferF1);
            DisposeHelper.Dispose(ref _bufferF2);
            DisposeHelper.Dispose(ref _bufferA);
            DisposeHelper.Dispose(ref _shadowBuffer);
            DisposeHelper.Dispose(ref _rasterizerState);
            CarNode.Dispose();
            Scene.Dispose();
            base.DisposeOverride();
        }
    }
}

