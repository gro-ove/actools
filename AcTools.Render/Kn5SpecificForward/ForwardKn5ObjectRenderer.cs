using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward.Materials;
using AcTools.Render.Shaders;
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
        private readonly string _carDirectory;

        public ForwardKn5ObjectRenderer(string mainKn5Filename, string carDirectory = null) {
            _carDirectory = carDirectory;
            Kn5 = Kn5.FromFile(mainKn5Filename);
        }

        private string _selectSkin;

        public void SelectPreviousSkin() {
            CarNode?.SelectPreviousSkin(DeviceContextHolder);
        }

        public void SelectNextSkin() {
            CarNode?.SelectNextSkin(DeviceContextHolder);
        }

        public void SelectSkin(string skinId) {
            CarNode?.SelectSkin(DeviceContextHolder, skinId);
        }

        private int? _selectLod;

        public int SelectedLod => _selectLod ?? (CarNode?.CurrentLod ?? -1);

        public int LodsCount => CarNode?.LodsCount ?? 0;

        public void SelectPreviousLod() {
            if (CarNode == null) return;
            SelectLod((CarNode.CurrentLod + CarNode.LodsCount - 1) % CarNode.LodsCount);
        }

        public void SelectNextLod() {
            if (CarNode == null) return;
            SelectLod((CarNode.CurrentLod + 1) % CarNode.LodsCount);
        }

        public void SelectLod(int lod) {
            if (CarNode == null) {
                _selectLod = lod;
                return;
            }

            CarNode.CurrentLod = lod;
            Scene.UpdateBoundingBox();

            IsDirty = true;
        }

        [CanBeNull]
        public Kn5RenderableCar CarNode { get; private set; }

        protected override void InitializeInner() {
            base.InitializeInner();

            DeviceContextHolder.Set<IMaterialsFactory>(new MaterialsProviderSimple());

            CarNode = new Kn5RenderableCar(Kn5, _carDirectory, Matrix.Identity, null);
            if (_selectLod.HasValue) {
                CarNode.CurrentLod = _selectLod.Value;
            }

            _selectLod = null;
            _selectSkin = null;

            OnPropertyChanged(nameof(CarNode));

            Scene.Add(CarNode);
            Scene.UpdateBoundingBox();

            Camera = CreateCamera(CarNode);
            _resetCamera = (CameraOrbit)Camera.Clone();
        }

        private static CameraOrbit CreateCamera(IRenderableObject node) {
            return new CameraOrbit(MathF.ToRadians(32f)) {
                Alpha = 0.9f,
                Beta = 0.1f,
                NearZ = 0.1f,
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
            get { return CarNode?.LightsEnabled == true; }
            set {
                if (CarNode != null) {
                    CarNode.LightsEnabled = value;
                }
            }
        }

        // crash test
        private int _i = -1;
        private Kn5RenderableCar _temp;

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(ActualCamera.Position);

            if (_i >= 0 && ++_i % 50 == 0) {
                if (_temp != null) {
                    Scene.Remove(_temp);
                    _temp.Dispose();
                }

                _temp = new Kn5RenderableCar(Kn5, _carDirectory,
                        Matrix.RotationY(MathUtils.Random(0f, 3.14f)) * Matrix.Translation(MathUtils.Random(-2f, 2f), 0f, MathUtils.Random(-2f, 2f)),
                        CarNode?.Skins?.RandomElement()) {
                            CurrentLod = Enumerable.Range(0, LodsCount).RandomElement(),
                            LightsEnabled = MathUtils.Random() > 0.5
                        };
                Scene.Add(_temp);
                Scene.UpdateBoundingBox();
            }
        }

        private TextBlockRenderer _textBlock;

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString($@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
Triangles: {CarNode?.TrianglesCount:D}
FXAA: {(UseFxaa ? "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}
Magick.NET: {(ImageUtils.IsMagickSupported ? "Yes" : "No")}".Trim(),
                    new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                    CoordinateType.Absolute);

            if (CarNode == null) return;

            var offset = 15;
            if (CarNode.LodsCount > 0) {
                var information = CarNode.CurrentLodInformation;
                _textBlock.DrawString($"LOD #{CarNode.CurrentLod + 1} ({CarNode.LodsCount} in total; shown from {information.In} to {information.Out})",
                        new RectangleF(0f, 0f, Width, Height - offset),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
                offset += 20;
            }

            var flags = new List<string>(4);

            if (CarNode.HasCockpitLr) {
                flags.Add(CarNode.CockpitLrActive ? "LR-cockpit" : "HR-cockpit");
            }

            if (CarNode.HasSeatbeltOn) {
                flags.Add(flags.Count > 0 ? ", seatbelt " : "Seatbelt ");
                flags.Add(CarNode.SeatbeltOnActive ? "is on" : "is off");
            }

            if (CarNode.HasBlurredNodes) {
                flags.Add(flags.Count > 0 ? ", blurred " : "Blurred ");
                flags.Add(CarNode.BlurredNodesActive ? "objects visible" : "objects hidden");
            }

            if (flags.Count > 0) {
                _textBlock.DrawString(flags.JoinToString(),
                        new RectangleF(0f, 0f, Width, Height - offset),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
                offset += 20;
            }

            if (CarNode.Skins != null && CarNode.CurrentSkin != null) {
                _textBlock.DrawString($"{CarNode.CurrentSkin} ({CarNode.Skins.IndexOf(CarNode.CurrentSkin) + 1}/{CarNode.Skins.Count})",
                        new RectangleF(0f, 0f, Width, Height - offset),
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
            base.Dispose();
        }
    }
}
