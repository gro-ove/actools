using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class AmbientShadowKn5ObjectRenderer : BaseRenderer {
        private readonly Kn5 _kn5;
        private readonly RenderableList _scene;
        private readonly CarData _carData;
        private RenderableList _carNode;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public AmbientShadowKn5ObjectRenderer(string mainKn5Filename, string carLocation = null) : this(Kn5.FromFile(mainKn5Filename), carLocation) {}

        public AmbientShadowKn5ObjectRenderer(Kn5 kn5, string carLocation = null) {
            _kn5 = kn5;
            _carData = new CarData(DataWrapper.FromDirectory(carLocation ?? Path.GetDirectoryName(kn5.OriginalFilename) ?? ""));
            _scene = new RenderableList();
        }

        public float DiffusionLevel = 0.35f;
        public float SkyBrightnessLevel = 4.0f;
        public float BodyMultipler = 0.8f;
        public float WheelMultipler = 0.69f;
        public float ClippingCoefficient = 10f;
        public float UpDelta = 0.1f;
        public int Iterations = 2000;
        public bool HideWheels = true;
        public bool Fade = true;
        public bool BlurResult = false;
        public bool DebugMode = false;

        public const int BodySize = 512;
        public const int BodyPadding = 64;
        public const int WheelSize = 64;
        public const int WheelPadding = 32;

        protected override void ResizeInner() { }

        private void LoadAndAdjustKn5() {
            DeviceContextHolder.Set<IMaterialsFactory>(new DepthMaterialsFactory());

            _carNode = (RenderableList)Kn5RenderableDepthOnlyObject.Convert(_kn5.RootNode);
            _scene.Add(_carNode);

            _carNode.UpdateBoundingBox();
            _carNode.LocalMatrix = Matrix.Translation(0, UpDelta - (_carNode.BoundingBox?.Minimum.Y ?? 0f), 0) * _carNode.LocalMatrix;
            _scene.UpdateBoundingBox();
        }

        private void LoadAmbientShadowSize() {
            _ambientBodyShadowSize = _carData.GetBodyShadowSize();
        }

        private void InitializeBuffers() {
            _shadowBuffer = TargetResourceDepthTexture.Create();
            _summBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _tempBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);

            _blendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = BlendOption.SourceAlpha,
                DestinationBlendAlpha = BlendOption.InverseSourceAlpha,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
            });

            _effect = DeviceContextHolder.GetEffect<EffectSpecialShadow>();
        }

        protected override void InitializeInner() {
            LoadAndAdjustKn5();
            LoadAmbientShadowSize();
            InitializeBuffers();
        }

        private void PrepareBuffers(int size, int shadowResolution) {
            Width = size;
            Height = size;
            Resize();

            _shadowBuffer.Resize(DeviceContextHolder, shadowResolution, shadowResolution, null);
            _shadowViewport = new Viewport(0, 0, _shadowBuffer.Width, _shadowBuffer.Height, 0, 1.0f);

            _summBuffer.Resize(DeviceContextHolder, size, size, null);
            _tempBuffer.Resize(DeviceContextHolder, size, size, null);
            DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, new Color4(0f, 0f, 0f, 0f));
        }

        private Vector3 _ambientBodyShadowSize, _shadowSize;
        private Viewport _shadowViewport;
        private TargetResourceDepthTexture _shadowBuffer;
        private CameraOrtho _shadowCamera;
        private Matrix _shadowDestinationTransform;
        private TargetResourceTexture _summBuffer, _tempBuffer;
        private BlendState _blendState;
        private EffectSpecialShadow _effect;
        private bool _wheelMode;

        private void DrawShadow(Vector3 from, Vector3? up = null) {
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            DeviceContext.Rasterizer.SetViewports(_shadowViewport);

            DeviceContext.ClearDepthStencilView(_shadowBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(_shadowBuffer.DepthView);

            _shadowCamera.LookAt(Vector3.Normalize(from) * _shadowCamera.FarZValue * 0.8f, Vector3.Zero, up ?? Vector3.UnitY);
            _shadowCamera.UpdateViewMatrix();

            if (HideWheels && !_wheelMode) {
                var wheelNodes = new[] {
                    "WHEEL_LF", "WHEEL_LR", "WHEEL_RF", "WHEEL_RR",
                    "HUB_LF", "HUB_LR", "HUB_RF", "HUB_RR",
                    "SUSP_LF", "SUSP_LR", "SUSP_RF", "SUSP_RR",
                };
                _scene.Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Simple, x => !wheelNodes.Contains((x as Kn5RenderableList)?.OriginalNode.Name));
            } else {
                _scene.Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Simple);
            }
        }

        private void AddShadow() {
            DeviceContext.OutputMerger.BlendState = _blendState;
            DeviceContext.Rasterizer.State = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);

            DeviceContext.OutputMerger.SetTargets(_summBuffer.TargetView);
            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);

            _effect.FxDepthMap.SetResource(_shadowBuffer.View);
            _effect.FxShadowViewProj.SetMatrix(_shadowDestinationTransform * _shadowCamera.ViewProj * new Matrix {
                M11 = 0.5f,
                M22 = -0.5f,
                M33 = 1.0f,
                M41 = 0.5f,
                M42 = 0.5f,
                M44 = 1.0f
            });
            _effect.TechAmbientShadow.DrawAllPasses(DeviceContext, 6);
        }

        private void Draw(float multipler, int size, int padding, bool fade) {
            DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, Color.Transparent);

            // draw
            var iter = 0f;
            for (var k = 0; k < Iterations; k++) {
                if (DebugMode) {
                    DrawShadow(Vector3.UnitY, Vector3.UnitZ);
                } else {
                    var x = MathF.Random(-1f, 1f);
                    var y = MathF.Random(0.1f, 1f) / DiffusionLevel.Clamp(0.001f, 1.0f);
                    var z = MathF.Random(-1f, 1f);

                    DrawShadow(new Vector3(x, y, z));
                }

                AddShadow();
                iter++;
            }

            DeviceContext.OutputMerger.BlendState = null;
            _effect.FxSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            // blurring
            for (var i = BlurResult ? 2 : 1; i > 0; i--) {
                _effect.FxMultipler.Set(i > 1 ? 2f : 1f);

                DeviceContext.ClearRenderTargetView(_tempBuffer.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_tempBuffer.TargetView);

                _effect.FxInputMap.SetResource(_summBuffer.View);
                _effect.TechHorizontalShadowBlur.DrawAllPasses(DeviceContext, 6);

                DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, Color.Transparent);
                DeviceContext.OutputMerger.SetTargets(_summBuffer.TargetView);

                _effect.FxInputMap.SetResource(_tempBuffer.View);
                _effect.TechVerticalShadowBlur.DrawAllPasses(DeviceContext, 6);
            }

            // result
            DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(RenderTargetView);
            _effect.FxInputMap.SetResource(_summBuffer.View);
            _effect.FxCount.Set(iter / SkyBrightnessLevel);
            _effect.FxMultipler.Set(multipler);
            _effect.FxPadding.Set(fade ? (float)padding / size : 0f);
            _effect.FxShadowSize.Set(new Vector2(_shadowSize.X, _shadowSize.Z) * ClippingCoefficient);
            _effect.TechResult.DrawAllPasses(DeviceContext, 6);
        }

        private void SaveResultAs(string outputDirectory, string name, int size, int padding) {
            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                stream.Position = 0;

                using (var image = Image.FromStream(stream))
                using (var target = new Bitmap(size, size))
                using (var g = Graphics.FromImage(target)) {
                    var cropRect = new Rectangle(padding, padding, size, size);
                    g.DrawImage(image, new Rectangle(0, 0, target.Width, target.Height),
                            cropRect, GraphicsUnit.Pixel);
                    target.Save(Path.Combine(outputDirectory, name));
                }
            }
        }

        private void SetBodyShadowCamera() {
            _shadowSize = _ambientBodyShadowSize * (1f + 2f * BodyPadding / BodySize);
            var size = Math.Max(_shadowSize.X, _shadowSize.Z) * 2f;
            _shadowCamera = new CameraOrtho {
                Width = size,
                Height = size,
                NearZ = 0.001f,
                FarZ = size + 20f,
                DisableFrustum = true
            };
            _shadowCamera.SetLens(1f);
            _shadowDestinationTransform = Matrix.Scaling(new Vector3(-_shadowSize.X, _shadowSize.Y, _shadowSize.Z)) * Matrix.RotationY(MathF.PI);
        }

        private void SetWheelShadowCamera() {
            _shadowSize = _carData.GetWheelShadowSize() * (1f + 2f * WheelPadding / WheelSize);
            var size = Math.Max(_shadowSize.X, _shadowSize.Z) * 2f;
            _shadowCamera = new CameraOrtho {
                Width = size,
                Height = size,
                NearZ = 0.001f,
                FarZ = size + 20f,
                DisableFrustum = true
            };
            _shadowCamera.SetLens(1f);
            _shadowDestinationTransform = Matrix.Scaling(new Vector3(-_shadowSize.X, _shadowSize.Y, _shadowSize.Z)) * Matrix.RotationY(MathF.PI);
        }

        private void BackupAndRecycle(string outputDirectory) {
            var original = new [] {
                "body", "tyre_0", "tyre_1", "tyre_2", "tyre_3"
            }.Select(x => Path.Combine(outputDirectory, x + "_shadow.png")).Select(x => new {
                Original = x,
                Backup = x.ApartFromLast(".png") + "~bak.png"
            }).ToList();

            try {
                foreach (var p in original) {
                    if (File.Exists(p.Original)) {
                        File.Move(p.Original, p.Backup);
                    }
                }
            } catch (Exception e) {
                throw new Exception("Cannot remove original files", e);
            }

            Task.Run(() => {
                foreach (var p in original) {
                    FileUtils.Recycle(p.Backup);
                }
            });
        }

        public void Shot(string outputDirectory) {
            if (!Initialized) {
                Initialize();
            }

            BackupAndRecycle(outputDirectory);

            // body shadow
            PrepareBuffers(BodySize + BodyPadding * 2, 1024);
            SetBodyShadowCamera();
            Draw(BodyMultipler, BodySize, BodyPadding, Fade);

            // return;
            SaveResultAs(outputDirectory, "body_shadow.png", BodySize, BodyPadding);

            // wheels shadows
            PrepareBuffers(WheelSize + WheelPadding * 2, 128);
            SetWheelShadowCamera();
            _wheelMode = true;

            var nodes = new[] { "WHEEL_LF", "WHEEL_RF", "WHEEL_LR", "WHEEL_RR" };
            foreach (var entry in nodes.Select(x => _carNode.GetDummyByName(x)).Select((x, i) => new {
                Node = x,
                Matrix = Matrix.Translation(0f, x.Matrix.GetTranslationVector().Y - (x.BoundingBox?.Minimum.Y ?? 0f), 0f),
                Filename = $"tyre_{i}_shadow.png"
            })) {
                _scene.Clear();
                _scene.Add(entry.Node);
                entry.Node.LocalMatrix = entry.Matrix;
                _scene.UpdateBoundingBox();

                Draw(WheelMultipler, WheelSize, WheelPadding, false);
                SaveResultAs(outputDirectory, entry.Filename, WheelSize, WheelPadding);
            }
        }

        public void Shot() {
            Shot(Path.GetDirectoryName(_kn5.OriginalFilename));
        }

        protected override void OnTick(float dt) { }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _blendState);
            DisposeHelper.Dispose(ref _summBuffer);
            DisposeHelper.Dispose(ref _tempBuffer);
            DisposeHelper.Dispose(ref _shadowBuffer);
            _carNode.Dispose();
            _scene.Dispose();
            base.Dispose();
        }
    }

    public class DepthMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (BasicMaterials.DepthOnlyKey.Equals(key)) {
                /* Model is loaded directly without using Kn5RenderableFile as a wrapper, so all materials
                 * keys won’t be converted to Kn5MaterialDescription. We don’t need any information about
                 * materials anyway. */
                return new Kn5MaterialDepth();
            }

            return new InvisibleMaterial();
        }
    }

    public class Kn5MaterialDepth : IRenderableMaterial {
        private EffectSpecialShadow _effect;

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSpecialShadow>();
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple) return false;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutP;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.States.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechSimplest.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() { }
    }

    public sealed class Kn5RenderableDepthOnlyObject : TrianglesRenderableObject<InputLayouts.VerticeP>, IKn5RenderableObject {
        public Kn5Node OriginalNode { get; }

        public Matrix ModelMatrixInverted { get; set; }

        public void SetMirrorMode(IDeviceContextHolder holder, bool enabled) {}

        public void SetDebugMode(IDeviceContextHolder holder, bool enabled) {}

        public void SetEmissive(Vector3? color) {}

        private static InputLayouts.VerticeP[] Convert(Kn5Node.Vertice[] vertices) {
            var size = vertices.Length;
            var result = new InputLayouts.VerticeP[size];
            
            for (var i = 0; i < size; i++) {
                var x = vertices[i];
                result[i] = new InputLayouts.VerticeP(x.Co.ToVector3());
            }

            return result;
        }

        private static ushort[] Convert(ushort[] indices) {
            return indices.ToIndicesFixX();
        }

        public Kn5RenderableDepthOnlyObject(Kn5Node node, bool forceVisible = false) : base(node.Name, Convert(node.Vertices), Convert(node.Indices)) {
            OriginalNode = node;
            if (IsEnabled && (!node.Active || !forceVisible && (!node.IsVisible || !node.IsRenderable))) {
                IsEnabled = false;
            }
        }

        private IRenderableMaterial _material;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            _material = contextHolder.Get<SharedMaterials>().GetMaterial(BasicMaterials.DepthOnlyKey);
            _material.Initialize(contextHolder);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return;
            if (!_material.Prepare(contextHolder, mode)) return;

            base.DrawOverride(contextHolder, camera, mode);

            _material.SetMatrices(ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override BaseRenderableObject Clone() {
            throw new NotSupportedException();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _material);
            base.Dispose();
        }

        public static IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, Convert);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableDepthOnlyObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}

