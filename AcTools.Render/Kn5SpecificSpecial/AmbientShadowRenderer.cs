using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class AmbientShadowRenderer : ShadowsRendererBase {
        public AmbientShadowRenderer([NotNull] string mainKn5Filename, [CanBeNull] string carLocation)
                : this(Kn5.FromFile(mainKn5Filename), DataWrapper.FromCarDirectory(carLocation ?? Path.GetDirectoryName(mainKn5Filename) ?? "")) {}

        public AmbientShadowRenderer([NotNull] Kn5 kn5, [CanBeNull] DataWrapper carData) : base(kn5, carData) {
            Iterations = 2000;
        }

        public float DiffusionLevel = 0.35f;
        public float UpDelta = 0.05f;
        public float SkyBrightnessLevel = 4.0f;
        public float BodyMultiplier = 0.8f;
        public float WheelMultiplier = 0.69f;
        public bool HideWheels = true;
        public bool Fade = true;
        public bool ExtraBlur = true;
        public bool CorrectLighting = true;

        public const int BodySize = 512;
        public const int BodyPadding = 64;
        public const int WheelSize = 64;
        public const int WheelPadding = 32;

        private void LoadAmbientShadowSize() {
            _ambientBodyShadowSize = CarData?.GetBodyShadowSize() ?? new Vector3(1f, 0f, 1f);
        }

        private void InitializeBuffers() {
            _shadowBuffer = TargetResourceDepthTexture.Create();
            _summBuffer = TargetResourceTexture.Create(Format.R32_Float);
            _tempBuffer = TargetResourceTexture.Create(Format.R32_Float);

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
            base.InitializeInner();
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

        private Kn5RenderableDepthOnlyObject[] _flattenNodes;

        private static readonly string[] WheelNodeNames = { "WHEEL", "HUB", "SUSP" };
        private static readonly string[] WheelNodeCorners = { "LF", "LR", "RF", "RR" };

        private void DrawShadow(Vector3 from, Vector3? up = null) {
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            DeviceContext.Rasterizer.SetViewports(_shadowViewport);

            DeviceContext.ClearDepthStencilView(_shadowBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(_shadowBuffer.DepthView);

            _shadowCamera.LookAt(Vector3.Normalize(from) * _shadowCamera.FarZValue * 0.8f, Vector3.Zero, up ?? Vector3.UnitY);
            _shadowCamera.UpdateViewMatrix();

            if (_flattenNodes == null) {
                string[] ignored;

                if (HideWheels && !_wheelMode) {
                    ignored = WheelNodeNames.SelectMany(x => WheelNodeCorners.Select(y => $"{x}_{y}")).Append("COCKPIT_HR", "STEER_HR").ToArray();
                } else {
                    ignored = new[] {
                        "COCKPIT_HR", "STEER_HR",
                    };
                }

                foreach (var wheel in WheelNodeCorners.Select(x => WheelNodeNames.Select(y => Scene.GetDummyByName($"{y}_{x}")).FirstOrDefault(y => y != null)).NonNull()) {
                    AcToolsLogging.Write("Moved up: " + wheel.Name);
                    wheel.ParentMatrix = Matrix.Translation(0, UpDelta, 0);
                }

                _flattenNodes = Flatten(Scene, x =>
                        (x as Kn5RenderableDepthOnlyObject)?.OriginalNode.CastShadows != false &&
                                !ignored.Contains((x as Kn5RenderableList)?.Name) && IsVisible(x))
                        .OfType<Kn5RenderableDepthOnlyObject>().ToArray();
            }

            for (var i = 0; i < _flattenNodes.Length; i++) {
                _flattenNodes[i].Draw(DeviceContextHolder, _shadowCamera, SpecialRenderMode.Simple);
            }
        }

        private float AddShadow(Vector3 lightDirection) {
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

            var brightness = CorrectLighting ? lightDirection.Y.Abs() : 1f;
            _effect.FxMultipler.Set(brightness);
            _effect.TechAmbientShadow.DrawAllPasses(DeviceContext, 6);
            return brightness;
        }

        protected override float DrawLight(Vector3 direction) {
            DrawShadow(direction);
            return AddShadow(-direction);
        }

        private void Draw(float multipler, int size, int padding, float fadeRadius, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            DeviceContext.ClearRenderTargetView(_summBuffer.TargetView, Color.Transparent);

            // draw
            var yThreshold = 0.95f - DiffusionLevel * 0.95f;
            ΘFromDeg = (MathF.PI / 2f - yThreshold.Acos()).ToDegrees();
            ΘToDeg = 90;

            var iter = DrawLights(progress, cancellation);
            if (cancellation.IsCancellationRequested) return;

            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);
            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);

            _effect.FxSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            // blurring
            _effect.FxMultipler.Set(1f);
            for (var i = ExtraBlur ? 2 : 1; i > 0; i--) {
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
            _effect.FxFade.Set(fadeRadius != 0f ? 10f / fadeRadius : 100f);
            _effect.FxPadding.Set(padding / (size + padding * 2f));
            _effect.FxShadowSize.Set(new Vector2(_shadowSize.X, _shadowSize.Z));
            _effect.FxScreenSize.Set(new Vector2(ActualWidth, ActualHeight));
            _effect.TechResult.DrawAllPasses(DeviceContext, 6);
        }

        private void SaveResultAs(string filename, int size, int padding) {
            using (var stream = new MemoryStream()) {
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                stream.Position = 0;

                using (var image = Image.FromStream(stream))
                using (var target = new Bitmap(size, size))
                using (var g = Graphics.FromImage(target)) {
                    var cropRect = new Rectangle(padding, padding, size, size);
                    g.DrawImage(image, new Rectangle(0, 0, target.Width, target.Height),
                            cropRect, GraphicsUnit.Pixel);
                    target.Save(filename);
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
            _shadowSize = CarData?.GetWheelShadowSize() * (1f + 2f * WheelPadding / WheelSize) ?? new Vector3(1f, 0f, 1f);
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

        public void Shot(string outputDirectory, [CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            if (!Initialized) {
                Initialize();
            }

            using (var replacement = FileUtils.RecycleOriginal(Path.Combine(outputDirectory, "body_shadow.png"))) {
                // body shadow
                PrepareBuffers(BodySize + BodyPadding * 2, 1024);
                SetBodyShadowCamera();
                Draw(BodyMultiplier, BodySize, BodyPadding, Fade ? 0.5f : 0f, progress.SubrangeDouble(0.01, 0.59), cancellation);
                if (cancellation.IsCancellationRequested) return;

                SaveResultAs(replacement.Filename, BodySize, BodyPadding);
            }

            // wheels shadows
            PrepareBuffers(WheelSize + WheelPadding * 2, 128);
            SetWheelShadowCamera();
            _wheelMode = true;

            var nodes = new[] { "WHEEL_LF", "WHEEL_RF", "WHEEL_LR", "WHEEL_RR" };
            var list = nodes.Select(x => CarNode.GetDummyByName(x)).NonNull().Select((x, i) => new {
                Node = x,
                GlobalMatrix = x.Matrix,
                Matrix = Matrix.Translation(-(CarData?.GetWheelGraphicOffset(x.Name) ?? Vector3.Zero) +
                        new Vector3(0f, x.Matrix.GetTranslationVector().Y - (x.BoundingBox?.Minimum.Y ?? 0f), 0f)),
                FileName = $"tyre_{i}_shadow.png",
                Progress = progress.SubrangeDouble(0.6 + i * 0.1, 0.099)
            }).ToList();

            foreach (var entry in list) {
                using (var replacement = FileUtils.RecycleOriginal(Path.Combine(outputDirectory, entry.FileName))) {
                    var m = Matrix.Invert(entry.GlobalMatrix);
                    _flattenNodes = list.SelectMany(x => {
                        x.Node.ParentMatrix = Matrix.Identity;
                        x.Node.LocalMatrix = entry.Matrix * x.GlobalMatrix * m;
                        return Flatten(x.Node).OfType<Kn5RenderableDepthOnlyObject>();
                    }).ToArray();

                    Draw(WheelMultiplier, WheelSize, WheelPadding, 1f, entry.Progress, cancellation);
                    if (cancellation.IsCancellationRequested) return;

                    SaveResultAs(replacement.Filename, WheelSize, WheelPadding);
                }
            }
        }

        public void Shot([CanBeNull] IProgress<double> progress, CancellationToken cancellation) {
            Shot(Path.GetDirectoryName(Kn5.OriginalFilename), progress, cancellation);
        }

        protected override void OnTickOverride(float dt) { }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _blendState);
            DisposeHelper.Dispose(ref _summBuffer);
            DisposeHelper.Dispose(ref _tempBuffer);
            DisposeHelper.Dispose(ref _shadowBuffer);
            CarNode.Dispose();
            Scene.Dispose();
            base.DisposeOverride();
        }
    }
}

