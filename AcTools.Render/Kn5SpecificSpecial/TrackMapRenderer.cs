using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using FillMode = SlimDX.Direct3D11.FillMode;
using Matrix = SlimDX.Matrix;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapRenderer : BaseRenderer {
        private readonly Kn5 _kn5;
        private Kn5RenderableList _trackNode;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public TrackMapRenderer(string mainKn5Filename) : this(Kn5.FromFile(mainKn5Filename, true)) { }

        public TrackMapRenderer(Kn5 kn5) {
            _kn5 = kn5;
        }

        protected override void ResizeInner() { }

        private Kn5MaterialsProvider _materialsProvider;

        protected override void InitializeInner() {
            _materialsProvider = new TrackMapMaterialProvider();
            DeviceContextHolder.Set(_materialsProvider);

            _materialsProvider.SetKn5(_kn5);
            _trackNode = (Kn5RenderableList)Kn5Converter.Convert(_kn5.RootNode, DeviceContextHolder);

            _trackNode.UpdateBoundingBox();
            if (_trackNode.BoundingBox.HasValue) {
                var size = _trackNode.BoundingBox.Value.GetSize();

                var width = size.X + Margin * 2;
                var height = size.X + Margin * 2;
            }
        }

        public bool UseFxaa = false;
        public float Multipler = 1f;

        public float XOffset, ZOffset,
            Margin = 2.0f, 
            ScaleFactor = 1.0f,
            DrawingSize = 10.0f;

        private void RenderTrackMap() {
            if (!_trackNode.BoundingBox.HasValue) return;

            var box = _trackNode.BoundingBox.Value;
            var camera = new CameraOrtho {
                Position = new Vector3(box.GetCenter().X, box.Maximum.Y + Margin, box.GetCenter().Z),
                FarZ = box.GetSize().Y + Margin * 2,
                Target = box.GetCenter(),
                Up = new Vector3(0.0f, 0.0f, -1.0f),
                Width = box.GetSize().X + Margin * 2,
                Height = box.GetSize().Z + Margin * 2
            };

            camera.SetLens(1f);

            _trackNode.Draw(DeviceContextHolder, camera, SpecialRenderMode.Simple, n => {
                if (n is RenderableList) return true;
                return n.Name?.IndexOf("ROAD", StringComparison.Ordinal) == 1;
            });
        }

        protected override void DrawInner() {
            using (var rasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsAntialiasedLineEnabled = false,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = false
            })) {
                DeviceContext.OutputMerger.BlendState = null;
                DeviceContext.Rasterizer.State = rasterizerState;
                DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);

                if (UseFxaa) {
                    using (var buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                        buffer.Resize(DeviceContextHolder, Width, Height);

                        DeviceContext.ClearRenderTargetView(buffer.TargetView, Color.Transparent);
                        DeviceContext.OutputMerger.SetTargets(buffer.TargetView);

                        RenderTrackMap();

                        DeviceContext.Rasterizer.State = null;
                        DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, buffer.View, RenderTargetView);
                    }
                } else {
                    DeviceContext.OutputMerger.SetTargets(RenderTargetView);
                    RenderTrackMap();
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

        public void Shot(string outputFile) {
            Debug.WriteLine("Shot: " + outputFile);

            if (!Initialized) {
                Initialize();
            }

            Width = (int)(Width * Multipler);
            Height = (int)(Height * Multipler);
            
            Draw();
            SaveResultAs(outputFile, 1f / Multipler);
        }

        protected override void OnTick(float dt) { }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _materialsProvider);
            base.Dispose();
        }
    }

    public class TrackMapMaterialProvider : Kn5MaterialsProvider {
        public override IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            return new Kn5MaterialTrackMap(kn5Material);
        }

        public override IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new InvisibleMaterial();
        }

        public override IRenderableMaterial CreateSkyMaterial() {
            return new InvisibleMaterial();
        }

        public override IRenderableMaterial CreateMirrorMaterial() {
            return new InvisibleMaterial();
        }
    }

    public class Kn5MaterialTrackMap : IRenderableMaterial {
        private EffectSpecialTrackMap _effect;

        internal Kn5MaterialTrackMap(Kn5Material material) { }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSpecialTrackMap>();
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Debug.WriteLine("Here");
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() { }
    }
}