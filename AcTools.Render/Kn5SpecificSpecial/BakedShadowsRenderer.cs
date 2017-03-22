using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class BakedShadowsRenderer : BaseRenderer {
        private readonly Kn5 _kn5;
        private readonly RenderableList _scene;
        private RenderableList _carNode;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public BakedShadowsRenderer(Kn5 kn5) {
            _kn5 = kn5;
            _scene = new RenderableList();
        }
        
        public float SkyBrightnessLevel = 2.5f;
        public float ΘFrom = -10.0f;
        public float ΘTo = 50.0f;
        public float Gamma = 0.5f;
        public float Ambient = 0.3f;
        public float UpDelta = 0.0f;
        public int Iterations = 500;
        public bool DebugMode = false;

        protected override void ResizeInner() { }

        private void LoadAndAdjustKn5() {
            DeviceContextHolder.Set<IMaterialsFactory>(new DepthMaterialsFactory());

            _carNode = (RenderableList)Kn5RenderableDepthOnlyObject.Convert(_kn5.RootNode);
            _scene.Add(_carNode);

            _carNode.UpdateBoundingBox();
            _carNode.LocalMatrix = Matrix.Translation(0, UpDelta - (_carNode.BoundingBox?.Minimum.Y ?? 0f), 0) * _carNode.LocalMatrix;
            _scene.UpdateBoundingBox();
        }

        private RasterizerState _rasterizerState;

        private void InitializeBuffers() {
            _shadowBuffer = TargetResourceDepthTexture.Create();
            _summBuffer = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
            _tempBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _tempBuffer2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _summBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = BlendOption.SourceAlpha,
                DestinationBlendAlpha = BlendOption.InverseSourceAlpha,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            });

            _bakedBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.SourceColor,
                DestinationBlend = BlendOption.DestinationColor,
                BlendOperation = BlendOperation.Maximum,
                SourceBlendAlpha = BlendOption.SourceAlpha,
                DestinationBlendAlpha = BlendOption.DestinationAlpha,
                BlendOperationAlpha = BlendOperation.Maximum,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            });

            _effect = DeviceContextHolder.GetEffect<EffectSpecialShadow>();

            _rasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                /*DepthBias = 10,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 0.5f*/
            });

            DeviceContextHolder.Set<INormalTexturesProvider>(new NormalTexturesProvider(_kn5));
        }

        protected override void InitializeInner() {
            LoadAndAdjustKn5();
            InitializeBuffers();
        }

        private void PrepareBuffers(int shadowResolution) {
            Resize();

            _shadowBuffer.Resize(DeviceContextHolder, shadowResolution, shadowResolution, null);
            _shadowViewport = new Viewport(0, 0, _shadowBuffer.Width, _shadowBuffer.Height, 0, 1.0f);

            _summBuffer.Resize(DeviceContextHolder, Width, Height, null);
            _tempBuffer.Resize(DeviceContextHolder, Width, Height, null);
            _tempBuffer2.Resize(DeviceContextHolder, Width, Height, null);
            DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, new Color4(0f, 0f, 0f, 0f));
        }
        
        private Viewport _shadowViewport;
        private TargetResourceDepthTexture _shadowBuffer;
        private CameraOrtho _shadowCamera;
        private TargetResourceTexture _summBuffer, _tempBuffer, _tempBuffer2;
        private BlendState _summBlendState, _bakedBlendState;
        private EffectSpecialShadow _effect;

        private Kn5RenderableDepthOnlyObject[] _flattenNodes;
        private Kn5RenderableDepthOnlyObject[] _filteredNodes;

        [NotNull]
        private static IEnumerable<Kn5RenderableDepthOnlyObject> Flatten(RenderableList root, Func<IRenderableObject, bool> filter = null) {
            return root
                    .SelectManyRecursive(x => {
                        var list = x as Kn5RenderableList;
                        if (list == null || !list.IsEnabled) return null;
                        return filter?.Invoke(list) == false ? null : list;
                    })
                    .OfType<Kn5RenderableDepthOnlyObject>()
                    .Where(x => x.IsEnabled && filter?.Invoke(x) != false);
        }

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
                _flattenNodes = Flatten(_scene).ToArray();
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

            DeviceContext.OutputMerger.SetTargets(_tempBuffer.TargetView);
            DeviceContext.ClearRenderTargetView(_tempBuffer.TargetView, new Color4(0f, 0f, 0f, 0f));
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
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _tempBuffer.View, _summBuffer.TargetView);
        }

        private void Draw(float multipler, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            _effect.FxAmbient.Set(Ambient);
            DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, Color.Transparent);

            /*var h = (int)Math.Round(Math.Pow(Iterations, 0.46));
            var v = (int)Math.Round(Math.Pow(Iterations, 0.54));
            var t = h * v;*/

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

            DeviceContext.ClearRenderTargetView(_tempBuffer.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(_tempBuffer.TargetView);
            _effect.FxInputMap.SetResource(_summBuffer.View);
            _effect.FxCount.Set(iter / SkyBrightnessLevel);
            _effect.FxMultipler.Set(multipler);
            _effect.FxGamma.Set(Gamma);
            _effect.TechAoResult.DrawAllPasses(DeviceContext, 6);

            for (var i = 0; i < 2; i++) {
                DeviceContext.ClearRenderTargetView(_tempBuffer2.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_tempBuffer2.TargetView);
                _effect.FxInputMap.SetResource(_tempBuffer.View);
                _effect.TechAoGrow.DrawAllPasses(DeviceContext, 6);

                DeviceContext.ClearRenderTargetView(_tempBuffer.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_tempBuffer.TargetView);
                _effect.FxInputMap.SetResource(_tempBuffer2.View);
                _effect.TechAoGrow.DrawAllPasses(DeviceContext, 6);
            }
            
            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _tempBuffer.View, RenderTargetView);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _tempBuffer.View, RenderTargetView);
            }
        }

        private void SetBodyShadowCamera() {
            var size = _carNode.BoundingBox?.GetSize().Length() ?? 4f;
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
        public float Multipler = 1.41f;

        public void Shot(string outputFile, string textureName, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            if (!Initialized) {
                Initialize();
                if (cancellation.IsCancellationRequested) return;
            }
            
            _filteredNodes = Flatten(_scene, x => {
                var kn5 = x as Kn5RenderableDepthOnlyObject;
                if (kn5 == null) return true;

                var material = _kn5.GetMaterial(kn5.OriginalNode.MaterialId);
                return material != null && material.TextureMappings.Any(m => m.Texture == textureName);
            }).ToArray();

            Width = (int)(Width * Multipler);
            Height = (int)(Height * Multipler);
            
            PrepareBuffers(2048);
            SetBodyShadowCamera();
            if (cancellation.IsCancellationRequested) return;

            Draw(1f, progress, cancellation);
            if (cancellation.IsCancellationRequested) return;

            SaveRenderBufferAsPng(outputFile, 1f / Multipler);
        }

        protected override void OnTick(float dt) { }

        private class NormalTexturesProvider : INormalTexturesProvider {
            private readonly Kn5 _kn5;

            public NormalTexturesProvider(Kn5 kn5) {
                _kn5 = kn5;
            }

            private Kn5TexturesProvider _texturesProvider;
            private readonly Dictionary<uint, Tuple<IRenderableTexture, float>[]> _nmTextures = new Dictionary<uint, Tuple<IRenderableTexture, float>[]>();

            Tuple<IRenderableTexture, float> INormalTexturesProvider.GetTexture(IDeviceContextHolder contextHolder, uint materialId) {
                Tuple<IRenderableTexture, float>[] result;
                if (!_nmTextures.TryGetValue(materialId, out result)) {
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

                    _nmTextures[materialId] = result;
                }

                return result[0];
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _texturesProvider);
                _nmTextures.Clear();
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _summBlendState);
            DisposeHelper.Dispose(ref _bakedBlendState);
            DisposeHelper.Dispose(ref _summBuffer);
            DisposeHelper.Dispose(ref _tempBuffer);
            DisposeHelper.Dispose(ref _tempBuffer2);
            DisposeHelper.Dispose(ref _shadowBuffer);
            DisposeHelper.Dispose(ref _rasterizerState);
            _carNode.Dispose();
            _scene.Dispose();
            base.Dispose();
        }
    }
}

