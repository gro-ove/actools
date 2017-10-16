using System.Diagnostics;
using System.Drawing;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using FillMode = SlimDX.Direct3D11.FillMode;
using Matrix = SlimDX.Matrix;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class UvRenderer : UtilsRendererBase {
        private readonly Kn5 _kn5;
        private Kn5RenderableFile _carNode;
        private IKn5RenderableObject[] _filteredNodes;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public UvRenderer(string mainKn5Filename) : this(Kn5.FromFile(mainKn5Filename)) {}

        public UvRenderer(Kn5 kn5) {
            _kn5 = kn5;
            Width = 2048;
            Height = 2048;
        }

        protected override void ResizeInner() {}

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new UvMaterialsFactory());
            _carNode = new Kn5RenderableFile(_kn5, Matrix.Identity);
        }

        public bool UseAntialiazing = true;
        public bool UseFxaa = false;

        private void RenderUv() {
            var effect = DeviceContextHolder.GetEffect<EffectSpecialUv>();

            var s = Stopwatch.StartNew();
            var j = 0;
            for (var x = -2f; x <= 2f; x++) {
                for (var y = -2f; y <= 2f; y++) {
                    effect.FxOffset.Set(new Vector2(x, y));
                    for (var i = 0; i < _filteredNodes.Length; i++) {
                        _filteredNodes[i].Draw(DeviceContextHolder, null, SpecialRenderMode.Simple);
                    }

                    j++;
                }
            }

            AcToolsLogging.Write($"Performance: {s.Elapsed.TotalMilliseconds / j:F1} ms per iteration; {j} iterations");
        }

        protected override void DrawOverride() {
            using (var rasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.None,
                IsAntialiasedLineEnabled = UseAntialiazing,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            })) {
                DeviceContext.OutputMerger.BlendState = null;
                DeviceContext.Rasterizer.State = rasterizerState;
                DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);

                using (var buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                    buffer.Resize(DeviceContextHolder, Width, Height, null);

                    DeviceContext.ClearRenderTargetView(buffer.TargetView, Color.Transparent);
                    DeviceContext.OutputMerger.SetTargets(buffer.TargetView);

                    RenderUv();

                    DeviceContext.Rasterizer.State = null;

                    PrepareForFinalPass();
                    if (UseFxaa) {
                        DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, buffer.View, RenderTargetView);
                    } else {
                        DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, buffer.View, RenderTargetView);
                    }
                }
            }
        }

        public void Shot(string outputFile, string textureName, string objectPath) {
            if (!Initialized) {
                Initialize();
            }

            _filteredNodes = Flatten(_kn5, _carNode, textureName, objectPath).ToArray();
            Draw();
            Texture2D.ToFile(DeviceContext, RenderBuffer, ImageFileFormat.Png, outputFile);
        }

        protected override void OnTickOverride(float dt) {}
    }

    public class UvMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            return new Kn5MaterialUv();
        }
    }

    public class Kn5MaterialUv : IRenderableMaterial {
        private EffectSpecialUv _effect;

        internal Kn5MaterialUv() {}

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSpecialUv>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.States.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) { }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() { }
    }
}
