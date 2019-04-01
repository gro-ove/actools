using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class BakedShadowsRenderer : ShadowsRendererBase {
        public BakedShadowsRenderer([NotNull] IKn5 kn5, [CanBeNull] DataWrapper carData) : base(kn5, carData) {
            ResolutionMultiplier = 2d;
        }

        public float SkyBrightnessLevel = 2.5f;
        public float Gamma = 0.5f;
        public float Ambient = 0.3f;
        public float ShadowBiasCullFront = 0f;
        public float ShadowBiasCullBack = 0.8f;
        public int Padding = 4;
        public int MapSize = 2048;
        public bool DebugMode = false;

        private RasterizerState _rasterizerStateFrontCull, _rasterizerStateBackCull;

        private void InitializeBuffers() {
            _shadowBuffer = TargetResourceDepthTexture.Create();
            _bufferFSum = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
            _bufferF1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _bufferF2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _bufferA = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _sumBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
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

            _rasterizerStateFrontCull = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = (int)(100 * ShadowBiasCullFront),
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = ShadowBiasCullFront
            });

            _rasterizerStateBackCull = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = (int)(100 * ShadowBiasCullBack),
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = ShadowBiasCullBack
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

            _bufferFSum.Resize(DeviceContextHolder, Width, Height, null);
            _bufferF1.Resize(DeviceContextHolder, Width, Height, null);
            _bufferF2.Resize(DeviceContextHolder, Width, Height, null);
            _bufferA.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            DeviceContext.ClearRenderTargetView(_bufferFSum.TargetView, new Color4(0f, 0f, 0f, 0f));
        }

        private Viewport _shadowViewport;
        private TargetResourceDepthTexture _shadowBuffer;
        private CameraOrtho _shadowCamera;
        private TargetResourceTexture _bufferFSum, _bufferF1, _bufferF2, _bufferA;
        private BlendState _sumBlendState, _bakedBlendState;
        private EffectSpecialShadow _effect;

        private Kn5RenderableDepthOnlyObject[] _flattenNodes;
        private UvProjectedObject[] _filteredNodes;

        private class UvProjectedObject {
            public static int OptionUvProjectMaximumArea = 10;

            public string Name => _mesh.Name;

            private readonly TrianglesRenderableObject<InputLayouts.VerticePT> _mesh;
            private readonly Vector2[] _offsets;

            public UvProjectedObject(TrianglesRenderableObject<InputLayouts.VerticePT> mesh) {
                _mesh = mesh;
                _offsets = GetOffsets(mesh).ToArray();
            }

            private static bool IsVertexWithin(Vector2 offset, Vector2 a) {
                return a.X >= offset.X && a.X <= offset.X + 1f &&
                        -a.Y >= offset.Y && -a.Y <= offset.Y + 1f;
            }

            private static bool IsOverlap(Vector2 offset, Vector2 a, Vector2 b, Vector2 c) {
                // TODO? Test if one of square vertices is within triangle?
                // TODO? Test if some edges do intersect?
                // Or is it only gonna be a waste of time since it’s unlikely UV is gonna be this tricky?
                return IsVertexWithin(offset, a) || IsVertexWithin(offset, b) || IsVertexWithin(offset, c);
            }

            private static bool IsOverlap(Vector2 offset, TrianglesRenderableObject<InputLayouts.VerticePT> obj) {
                for (var i = 2; i < obj.IndicesCount; i += 3) {
                    if (IsOverlap(offset, obj.Vertices[obj.Indices[i - 2]].Tex, obj.Vertices[obj.Indices[i - 1]].Tex, obj.Vertices[obj.Indices[i]].Tex)) {
                        return true;
                    }
                }

                return false;
            }

            private static IEnumerable<Vector2> GetOffsets(TrianglesRenderableObject<InputLayouts.VerticePT> obj) {
                for (var o = new Vector2(-OptionUvProjectMaximumArea); o.X <= OptionUvProjectMaximumArea; o.X++) {
                    for (o.Y = -OptionUvProjectMaximumArea; o.Y <= OptionUvProjectMaximumArea; o.Y++) {
                        if (IsOverlap(o, obj)) {
                            yield return o;
                        }
                    }
                }
            }

            public void Draw(IDeviceContextHolder holder, EffectOnlyVector2Variable offset, SpecialRenderMode renderMode) {
                for (var i = 0; i < _offsets.Length; i++) {
                    offset.Set(_offsets[i]);
                    _mesh.Draw(holder, null, renderMode);
                }
            }
        }

        private void DrawShadow(Vector3 from, Vector3? up = null) {
            from.Normalize();
            _effect.FxLightDir.Set(from);

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
                var states = new[] { _rasterizerStateFrontCull, _rasterizerStateBackCull };
                _flattenNodes.ForEach(x => x.SeveralRasterizerStates = states);
            }

            for (var i = 0; i < _flattenNodes.Length; i++) {
                _flattenNodes[i].Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Simple);
            }
        }

        private void RenderPieces() {
            for (var i = 0; i < _filteredNodes.Length; i++) {
                _filteredNodes[i].Draw(DeviceContextHolder, _effect.FxOffset, SpecialRenderMode.Shadow);
            }
        }

        private void AddShadow() {
            DeviceContext.OutputMerger.BlendState = _bakedBlendState;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.State = null;
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

            RenderPieces();
            // DeviceContext.Rasterizer.State = null;

            // copy to summary buffer
            DeviceContext.OutputMerger.BlendState = _sumBlendState;
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _bufferF1.View, _bufferFSum.TargetView);
        }

        protected override float DrawLight(Vector3 direction) {
            DrawShadow(direction);
            AddShadow();
            return 1f;
        }

        private void Draw(float multiplier, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            _effect.FxAmbient.Set(Ambient);
            DeviceContext.ClearRenderTargetView(_bufferFSum.TargetView, Color.Transparent);
            DeviceContext.ClearRenderTargetView(_bufferA.TargetView, Color.Transparent);

            // draw
            var t = DrawLights(progress, cancellation);
            if (cancellation.IsCancellationRequested) return;

            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);
            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);
            _effect.FxSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            DeviceContext.ClearRenderTargetView(_bufferF1.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(_bufferF1.TargetView);
            _effect.FxInputMap.SetResource(_bufferFSum.View);
            _effect.FxCount.Set(t / SkyBrightnessLevel);
            _effect.FxMultipler.Set(multiplier);
            _effect.FxGamma.Set(Gamma);
            _effect.FxScreenSize.Set(new Vector2(Width, Height));
            _effect.TechAoResult.DrawAllPasses(DeviceContext, 6);

            for (var i = 0; i < Padding; i++) {
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

            if (FullyTransparent) {
                DeviceContextHolder.GetHelper<CopyHelper>().DrawFullyTransparent(DeviceContextHolder, _bufferA.View, RenderTargetView);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _bufferA.View, RenderTargetView);
            }
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

        public bool UseFxaa = true;
        public bool FullyTransparent = false;

        [CanBeNull]
        public byte[] Shot(string textureName, [CanBeNull] string objectPath, [CanBeNull] IProgress<double> progress,
                CancellationToken cancellation) {
            if (!Initialized) {
                Initialize();
                if (cancellation.IsCancellationRequested) return null;
            }

            _filteredNodes = Flatten(Kn5, Scene, textureName, objectPath).OfType<TrianglesRenderableObject<InputLayouts.VerticePT>>()
                                                                         .Select(x => new UvProjectedObject(x)).ToArray();
            AcToolsLogging.Write("Filtered nodes:\n" + _filteredNodes.Select(x => x.Name).JoinToString('\n'));

            PrepareBuffers(MapSize);
            SetBodyShadowCamera();
            if (cancellation.IsCancellationRequested) return null;

            Draw(1f, progress, cancellation);
            if (cancellation.IsCancellationRequested) return null;

            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                return stream.ToArray();
            }
        }

        protected override void OnTickOverride(float dt) { }

        private class NormalsNormalsTexturesProvider : INormalsNormalTexturesProvider {
            private readonly IKn5 _kn5;

            public NormalsNormalsTexturesProvider(IKn5 kn5) {
                _kn5 = kn5;
            }

            private Kn5TexturesProvider _texturesProvider;
            private readonly Dictionary<uint, Tuple<IRenderableTexture, float>[]> _cache = new Dictionary<uint, Tuple<IRenderableTexture, float>[]>();

            Tuple<IRenderableTexture, float> INormalsNormalTexturesProvider.GetTexture(IDeviceContextHolder contextHolder, uint materialId) {
                if (!_cache.TryGetValue(materialId, out var result)) {
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
            DisposeHelper.Dispose(ref _sumBlendState);
            DisposeHelper.Dispose(ref _bakedBlendState);
            DisposeHelper.Dispose(ref _bufferFSum);
            DisposeHelper.Dispose(ref _bufferF1);
            DisposeHelper.Dispose(ref _bufferF2);
            DisposeHelper.Dispose(ref _bufferA);
            DisposeHelper.Dispose(ref _shadowBuffer);
            DisposeHelper.Dispose(ref _rasterizerStateFrontCull);
            DisposeHelper.Dispose(ref _rasterizerStateBackCull);
            CarNode.Dispose();
            Scene.Dispose();
            base.DisposeOverride();
        }
    }
}

