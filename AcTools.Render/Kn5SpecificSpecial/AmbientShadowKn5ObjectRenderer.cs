using System;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class AmbientShadowKn5ObjectRenderer : ForwardRenderer {
        private readonly Kn5 _kn5;
        private readonly CarHelper _carHelper;

        public AmbientShadowKn5ObjectRenderer(string mainKn5Filename) {
            _kn5 = Kn5.FromFile(mainKn5Filename);
            _carHelper = new CarHelper(_kn5);
        }

        public const int Size = 512;
        public const int Padding = 128;

        protected override void InitializeInner() {
            base.InitializeInner();

            Kn5MaterialsProvider.Initialize(new DepthMaterialProvider());
            Kn5MaterialsProvider.SetKn5(_kn5);

            var node = Kn5Converter.Convert(_kn5.RootNode);
            Scene.Add(node);

            var asList = node as Kn5RenderableList;
            if (asList != null) {
                _carHelper.AdjustPosition(asList);
            }

            Scene.UpdateBoundingBox();
            if (node.BoundingBox == null) throw new Exception("Invalid mesh (can't calculate BB)");
            
            var bb = node.BoundingBox.Value;
            // AmbientBodyShadowSize = bb.GetSize() + new Vector3(0.4f, 0f, 0.4f);

            var iniFile = _carHelper.Data.GetIniFile("ambient_shadows.ini");
            AmbientBodyShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));

            AmbientBodyShadowSize.X *= 1f + 2f * Padding / Size;
            AmbientBodyShadowSize.Z *= 1f + 2f * Padding / Size;

            //AmbientBodyShadowSize.X = (bb.GetSize().X + 0.2f) / 2f;
            //AmbientBodyShadowSize.Z = (bb.GetSize().Z + 0.2f) / 2f;

            Camera = new CameraOrtho {
                NearZ = 0.001f,
                FarZ = bb.GetSize().Y + 0.02f,
                Width = AmbientBodyShadowSize.X * 2f,
                Height = AmbientBodyShadowSize.Z * 2f
            };

            ((CameraOrtho)Camera).LookAt(new Vector3(0f, -0.01f, 0f), Vector3.Zero, Vector3.UnitZ);
            ((CameraOrtho)Camera).SetLens(1f);
        }

        protected override void DrawInner() {
            // TODO: disposal
            var buffer = TargetResourceDepthTexture.Create();
            buffer.Resize(DeviceContextHolder, Width, Height);

            var doubleSided = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsFrontCounterclockwise = true,
                IsDepthClipEnabled = false
            });

            // prepare
            DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);
            DeviceContext.ClearDepthStencilView(buffer.StencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(buffer.StencilView);

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;

            DeviceContext.Rasterizer.State = doubleSided;

            // draw
            DrawPrepare();

            foreach (var node in new [] {
                "WHEEL_LF", "WHEEL_LR", "WHEEL_RF", "WHEEL_RR",
                "SUSP_LF", "SUSP_LR", "SUSP_RF", "SUSP_RR",
            }.Select(x => Scene.GetDummyByName(x)).Where(x => x != null)) {
                node.IsEnabled = false;
            }

            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Simple);

            // disposion
            DeviceContext.Rasterizer.State = null;
            doubleSided.Dispose();

            // prepare
            var effect = DeviceContextHolder.GetEffect<EffectSpecialShadow>();
            DeviceContextHolder.PrepareQuad(effect.LayoutPT);
            effect.FxSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            var debug = true;
            if (debug) {
                DeviceContext.OutputMerger.SetTargets(RenderTargetView);
                effect.FxDepthMap.SetResource(buffer.View);
                effect.TechBase.DrawAllPasses(DeviceContext, 6);
                return;
            }

            // base
            var temp0 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            temp0.Resize(DeviceContextHolder, Width, Height);

            DeviceContext.ClearRenderTargetView(temp0.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(temp0.TargetView);

            effect.FxDepthMap.SetResource(buffer.View);
            effect.TechBase.DrawAllPasses(DeviceContext, 6);

            // blur
            var temp1 = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            temp1.Resize(DeviceContextHolder, Width, Height);

            for (var i = 0; i < 2; i++) {
                DeviceContext.ClearRenderTargetView(temp1.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(temp1.TargetView);

                effect.FxInputMap.SetResource(temp0.View);
                effect.TechHorizontalShadowBlur.DrawAllPasses(DeviceContext, 6);

                DeviceContext.ClearRenderTargetView(temp0.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(temp0.TargetView);

                effect.FxInputMap.SetResource(temp1.View);
                effect.TechVerticalShadowBlur.DrawAllPasses(DeviceContext, 6);
            }

            // result
            DeviceContext.OutputMerger.SetTargets(RenderTargetView);
            effect.FxInputMap.SetResource(temp0.View);
            effect.TechFinal.DrawAllPasses(DeviceContext, 6);
        }

        public Vector3 AmbientBodyShadowSize;

        public void Shot(string outputDirectory) {
            Initialize();

            Width = Size + Padding * 2;
            Height = Size + Padding * 2;

            Draw();

            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Jpg, stream);
                stream.Position = 0;
                var image = Image.FromStream(stream);

                var cropRect = new Rectangle(Padding, Padding, Size, Size);
                var target = new Bitmap(Size, Size);
                using (var g = Graphics.FromImage(target)) {
                    g.DrawImage(image, new Rectangle(0, 0, target.Width, target.Height),
                            cropRect,
                            GraphicsUnit.Pixel);
                }

                target.Save(Path.Combine(outputDirectory, "body_shadow.png"));
            }
        }

        protected override void OnTick(float dt) {}

        public override void Dispose() {
            _carHelper.Dispose();
            base.Dispose();
        }
    }
}
