using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using FillMode = SlimDX.Direct3D11.FillMode;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class UvRenderer : BaseRenderer {
        private readonly Kn5 _kn5;
        private Kn5RenderableList _carNode;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public UvRenderer(string mainKn5Filename) : this(Kn5.FromFile(mainKn5Filename)) {}

        public UvRenderer(Kn5 kn5) {
            _kn5 = kn5;
            Width = 2048;
            Height = 2048;
        }

        protected override void ResizeInner() {}

        private Kn5MaterialsProvider _materialsProvider;

        protected override void InitializeInner() {
            _materialsProvider = new UvMaterialProvider();
            DeviceContextHolder.Set(_materialsProvider);

            _materialsProvider.SetKn5(_kn5);
            _carNode = (Kn5RenderableList)Kn5Converter.Convert(_kn5.RootNode, DeviceContextHolder);
        }

        public bool UseAntialiazing = true;
        public bool UseFxaa = false;
        public float Multipler = 1f;

        private void RenderUv() {
            var effect = DeviceContextHolder.GetEffect<EffectSpecialUv>();

            for (var x = -1f; x <= 1f; x++) {
                for (var y = -1f; y <= 1f; y++) {
                    effect.FxOffset.Set(new Vector2(x, y));
                    _carNode.Draw(DeviceContextHolder, null, SpecialRenderMode.Simple);
                }
            }
        }

        protected override void DrawInner() {
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

                if (UseFxaa) {
                    using (var buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                        buffer.Resize(DeviceContextHolder, Width, Height);

                        DeviceContext.ClearRenderTargetView(buffer.TargetView, Color.Transparent);
                        DeviceContext.OutputMerger.SetTargets(buffer.TargetView);

                        RenderUv();

                        DeviceContext.Rasterizer.State = null;
                        DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, buffer.View, RenderTargetView);
                    }
                } else {
                    DeviceContext.OutputMerger.SetTargets(RenderTargetView);
                    RenderUv();
                }
            }
        }

        private void SaveResultAs(string filename, float multipler) {
            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                stream.Position = 0;

                using (var image = Image.FromStream(stream)) {
                    if (Equals(multipler, 1f)) {
                        image.Save(filename, ImageFormat.Png);
                    } else {
                        using (var bitmap = new Bitmap((int)(Width * multipler), (int)(Height * multipler)))
                        using (var graphics = Graphics.FromImage(bitmap)) {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.DrawImage(image, 0f, 0f, Width * multipler, Height * multipler);

                            bitmap.Save(filename, ImageFormat.Png);
                        }
                    }
                }
            }
        }

        public void Shot(string outputFile, string textureName) {
            if (!Initialized) {
                Initialize();
            }

            Width = (int)(Width * Multipler);
            Height = (int)(Height * Multipler);

            Kn5MaterialUv.Filter = textureName;
            Draw();
            SaveResultAs(outputFile, 1f / Multipler);
        }

        protected override void OnTick(float dt) {}

        public override void Dispose() {
            DisposeHelper.Dispose(ref _materialsProvider);
            base.Dispose();
        }
    }
}
