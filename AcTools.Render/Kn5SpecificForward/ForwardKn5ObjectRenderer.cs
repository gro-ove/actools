using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectWrite;
using SpriteTextRenderer;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = SpriteTextRenderer.TextAlignment;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.Kn5SpecificForward {
    public class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        public bool AutoRotate { get; set; } = true;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool VisibleUi { get; set; } = true;

        private bool _useFpsCamera;

        public bool UseFpsCamera {
            get { return _useFpsCamera; }
            set {
                if (Equals(value, _useFpsCamera)) return;
                _useFpsCamera = value;
                OnPropertyChanged();

                if (value) {
                    var orbit = CameraOrbit ?? CreateCamera(Scene);
                    Camera = new FpsCamera(orbit.FovY) {
                        NearZ = orbit.NearZ,
                        FarZ = orbit.FarZ
                    };

                    Camera.LookAt(orbit.Position, orbit.Target, orbit.Up);
                } else {
                    Camera = _resetCamera.Clone();
                }

                Camera.SetLens(AspectRatio);
            }
        }

        public Kn5 Kn5 { get; }

        protected CarHelper CarHelper { get; }

        public ForwardKn5ObjectRenderer(string mainKn5Filename, string carDirectory = null) {
            Kn5 = Kn5.FromFile(mainKn5Filename);
            CarHelper = new CarHelper(Kn5, carDirectory);
        }

        private string _selectSkin;
        public string CurrentSkin => CarHelper.CurrentSkin;

        public void SelectPreviousSkin() {
            if (MaterialsProvider == null) return;
            CarHelper.SelectPreviousSkin(DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        public void SelectNextSkin() {
            if (MaterialsProvider == null) return;
            CarHelper.SelectNextSkin(DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        public void SelectSkin(string skinId) {
            if (MaterialsProvider == null) {
                _selectSkin = skinId;
                return;
            }

            CarHelper.SelectSkin(skinId, DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        protected Kn5MaterialsProvider MaterialsProvider;
        protected TexturesProvider TexturesProvider;

        [CanBeNull]
        protected Kn5RenderableList CarNode;

        [CanBeNull]
        private List<CarLight> _carLights;

        protected override void InitializeInner() {
            base.InitializeInner();

            MaterialsProvider = new MaterialsProviderSimple();
            TexturesProvider = new TexturesProvider();
            DeviceContextHolder.Set(MaterialsProvider);
            DeviceContextHolder.Set(TexturesProvider);
            
            CarHelper.SetKn5(DeviceContextHolder);
            CarHelper.SkinTextureUpdated += (sender, args) => IsDirty = true;

            var node = Kn5Converter.Convert(Kn5.RootNode, DeviceContextHolder);
            Scene.Add(node);

            CarNode = node as Kn5RenderableList;
            if (CarNode != null) {
                Scene.InsertRange(0, CarHelper.LoadAmbientShadows(CarNode, 0f));
                CarHelper.AdjustPosition(CarNode);
                CarHelper.LoadMirrors(CarNode, DeviceContextHolder);

                _carLights = CarHelper.LoadLights(CarNode).ToList();
            }

            Scene.UpdateBoundingBox();
            TrianglesCount = node.TrianglesCount;
            ObjectsCount = node.ObjectsCount;

            Camera = CreateCamera(node);
            _resetCamera = (CameraOrbit)Camera.Clone();

            if (_selectSkin != null) {
                SelectSkin(_selectSkin);
                _selectSkin = null;
            }
        }

        private static CameraOrbit CreateCamera(IRenderableObject node) {
            return new CameraOrbit(MathF.ToRadians(32f)) {
                Alpha = 0.9f,
                Beta = 0.1f,
                NearZ = 0.02f,
                FarZ = 300f,
                Radius = node?.BoundingBox?.GetSize().Length() ?? 4.8f,
                Target = (node?.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.05f, 0f)
            };
        }

        private float _resetState;
        private CameraOrbit _resetCamera;

        public void ResetCamera() {
            UseFpsCamera = false;
            AutoRotate = true;
            _resetState = 1f;
        }

        public bool CarLightsEnabled {
            get { return _carLights?.FirstOrDefault()?.IsEnabled == true; }
            set {
                if (_carLights == null) return;
                foreach (var light in _carLights) {
                    if (light.IsEnabled == value) return;
                    light.IsEnabled = value;
                }
                IsDirty = true;
                OnPropertyChanged(nameof(CarLightsEnabled));
            }
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(ActualCamera.Position);
        }

        private TextBlockRenderer _textBlock;

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString($@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
Triangles: {TrianglesCount:D}
FXAA: {(UseFxaa ? "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}
Magick.NET: {(ImageUtils.IsMagickSupported ? "Yes" : "No")}".Trim(),
                    new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                    CoordinateType.Absolute);

            if (CarHelper.Skins != null && CarHelper.CurrentSkin != null) {
                _textBlock.DrawString($"{CarHelper.CurrentSkin} ({CarHelper.Skins.IndexOf(CarHelper.CurrentSkin) + 1}/{CarHelper.Skins.Count})",
                        new RectangleF(0f, 0f, Width, Height - 20),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }
        }

        private float _elapsedCamera;

        protected override void OnTick(float dt) {
            base.OnTick(dt);

            const float threshold = 0.001f;
            if (_resetState > threshold) {
                if (!AutoRotate) {
                    _resetState = 0f;
                    return;
                }

                AutoAdjustTarget = true;

                _resetState += (-0f - _resetState) / 10f;
                if (_resetState <= threshold) {
                    AutoRotate = false;
                }

                var cam = CameraOrbit;
                if (cam != null) {
                    cam.Alpha += (_resetCamera.Alpha - cam.Alpha) / 10f;
                    cam.Beta += (_resetCamera.Beta - cam.Beta) / 10f;
                    cam.Radius += (_resetCamera.Radius - cam.Radius) / 10f;
                    cam.FovY += (_resetCamera.FovY - cam.FovY) / 10f;
                }

                _elapsedCamera = 0f;

                IsDirty = true;
            } else if (AutoRotate && CameraOrbit != null) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta += (MathF.Sin(_elapsedCamera * 0.39f) * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;

                IsDirty = true;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = _resetCamera.Target + new Vector3(-0.2f * CameraOrbit.Position.X, -0.1f * CameraOrbit.Position.Y, 0f);
                CameraOrbit.Target += (t - CameraOrbit.Target) / 2f;
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _textBlock);
            CarHelper.Dispose();
            base.Dispose();
        }
    }
}
