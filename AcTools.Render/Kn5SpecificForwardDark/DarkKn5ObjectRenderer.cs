using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SpriteTextRenderer;
using FontStretch = SlimDX.DirectWrite.FontStretch;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using FontWeight = SlimDX.DirectWrite.FontWeight;
using MaterialsProviderSimple = AcTools.Render.Kn5SpecificForwardDark.Materials.MaterialsProviderSimple;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        public bool AutoRotate { get; set; } = true;

        public bool PauseRotation { get; set; } = false;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool CubemapReflection { get; set; } = false;

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

        private Vector3 _light = Vector3.Normalize(new Vector3(0.2f, 1.0f, 0.8f));
        private ReflectionCubemap _reflectionCubemap;
        private ShadowsDirectional _shadows;

        [CanBeNull]
        private string _carKn5;

        [CanBeNull]
        private string _showroomKn5;

        [CanBeNull]
        public Kn5RenderableCar CarNode { get; private set; }

        private RenderableList _carWrapper;

        public DarkKn5ObjectRenderer(string carKn5, string showroomKn5 = null) {
            _carKn5 = carKn5;
            _showroomKn5 = showroomKn5;
            
            UseMsaa = false;
            WpfMode = true;
            VisibleUi = false;
            UseSprite = false;
            BackgroundColor = Color.FromArgb(10, 15, 25);
        }

        private string _loadingCarKn5;

        private class PreviousCar {
            public string Id;
            public List<IRenderableObject> Objects;
        }

        private readonly List<PreviousCar> _previousCars = new List<PreviousCar>(2);

        private void ClearExisting() {
            if (_carKn5 != null) {
                var existing = _previousCars.FirstOrDefault(x => x.Id == _carKn5);
                if (existing != null) {
                    _previousCars.Remove(existing);
                    _previousCars.Add(existing);
                } else if (_carWrapper.OfType<Kn5RenderableCar>().Any()) {
                    if (_previousCars.Count >= 2) {
                        var toRemoval = _previousCars[0];
                        toRemoval.Objects.DisposeEverything();
                        _previousCars.RemoveAt(0);
                    }

                    _previousCars.Add(new PreviousCar {
                        Id = _carKn5,
                        Objects = _carWrapper.ToList()
                    });

                    _carWrapper.Clear();
                    return;
                }
            }

            _carWrapper.DisposeEverything();
        }

        public async Task SetCarAsync(string carKn5, string skinId = Kn5RenderableCar.DefaultSkin, 
                CancellationToken cancellationToken = default(CancellationToken)) {
            LicensePlateSelected = false;

            try {
                _loadingCarKn5 = carKn5;

                if (_carWrapper == null) {
                    _carKn5 = carKn5;
                    return;
                }

                if (carKn5 == null) {
                    ClearExisting();
                    _carKn5 = null;
                    CarNode = null;
                    Scene.UpdateBoundingBox();
                    return;
                }

                Kn5RenderableCar loaded = null;
                FlatMirror reflection = null;

                var previous = _previousCars.FirstOrDefault(x => x.Id == carKn5);
                if (previous != null) {
                    _previousCars.Remove(previous);

                    ClearExisting();
                    _carWrapper.AddRange(previous.Objects);
                    _carKn5 = carKn5;
                    loaded = previous.Objects.OfType<Kn5RenderableCar>().First();
                    loaded.LightsEnabled = CarNode?.LightsEnabled == true;
                    CarNode = loaded;
                    if (_selectSkinLater) {
                        CarNode.SelectSkin(DeviceContextHolder, _selectSkin);
                        _selectSkinLater = false;
                    } else {
                        CarNode.SelectSkin(DeviceContextHolder, skinId);
                    }
                    Scene.UpdateBoundingBox();
                    return;
                }

                await Task.Run(() => {
                    var carKn5Loaded = Kn5.FromFile(carKn5);
                    if (cancellationToken.IsCancellationRequested) return;

                    loaded = new Kn5RenderableCar(carKn5Loaded, Path.GetDirectoryName(carKn5), Matrix.Identity, _selectSkinLater ? _selectSkin : skinId);
                    _selectSkinLater = false;
                    if (cancellationToken.IsCancellationRequested) return;

                    loaded.LightsEnabled = CarNode?.LightsEnabled == true;
                    if (cancellationToken.IsCancellationRequested) return;

                    loaded.Draw(DeviceContextHolder, null, SpecialRenderMode.InitializeOnly);
                    if (cancellationToken.IsCancellationRequested) return;

                    reflection = new FlatMirror(loaded, new Plane(Vector3.Zero, Vector3.UnitY));
                    reflection.Draw(DeviceContextHolder, null, SpecialRenderMode.InitializeOnly);
                });

                if (cancellationToken.IsCancellationRequested || _loadingCarKn5 != carKn5) {
                    loaded?.Dispose();
                    return;
                }

                ClearExisting();

                _carWrapper.Add(loaded);
                _carWrapper.Add(reflection);
                _carKn5 = carKn5;
                CarNode = loaded;
                _selectSkin = null;
                Scene.UpdateBoundingBox();
            } catch (Exception e) {
                MessageBox.Show(e.ToString());
                throw;
            } finally {
                if (ReferenceEquals(_loadingCarKn5, carKn5)) {
                    _loadingCarKn5 = null;
                }
            }
        }

        private bool _selectSkinLater;
        private string _selectSkin = Kn5RenderableCar.DefaultSkin;

        public void SelectPreviousSkin() {
            CarNode?.SelectPreviousSkin(DeviceContextHolder);
        }

        public void SelectNextSkin() {
            CarNode?.SelectNextSkin(DeviceContextHolder);
        }

        public void SelectSkin(string skinId) {
            if (CarNode == null || _loadingCarKn5 != null) {
                _selectSkin = skinId;
                _selectSkinLater = true;
                return;
            }

            CarNode.SelectSkin(DeviceContextHolder, skinId);
        }

        protected override void InitializeInner() {
            base.InitializeInner();

            DeviceContextHolder.Set<IMaterialsFactory>(new MaterialsProviderSimple());

            if (_showroomKn5 != null) {
                var kn5 = Kn5.FromFile(_showroomKn5);
                Scene.Insert(0, new Kn5RenderableFile(kn5, Matrix.Identity));
            }

            _carWrapper = new RenderableList();
            Scene.Add(_carWrapper);

            if (_carKn5 != null) {
                var kn5 = Kn5.FromFile(_carKn5);
                CarNode = new Kn5RenderableCar(kn5, Path.GetDirectoryName(_carKn5), Matrix.Identity, _selectSkinLater ? _selectSkin : Kn5RenderableCar.DefaultSkin);
                _selectSkinLater = false;
                _carWrapper.Add(CarNode);

                var reflection = new FlatMirror(CarNode, new Plane(Vector3.Zero, Vector3.UnitY));
                _carWrapper.Add(reflection);
            }

            Scene.UpdateBoundingBox();

            if (CubemapReflection) {
                _reflectionCubemap = new ReflectionCubemap(1024);
                _reflectionCubemap.Initialize(DeviceContextHolder);
            }

            if (EffectDarkMaterial.EnableShadows) {
                _shadows = new ShadowsDirectional(2048, new[] { 5f });
                _shadows.Initialize(DeviceContextHolder);
            }

            if (Camera == null) {
                Camera = CreateCamera(CarNode);
                _resetCamera = (CameraOrbit)Camera.Clone();
            }
        }

        private class SpecialCamera : CameraOrbit {
            public SpecialCamera(float fov) : base(fov) { }

            public override bool Visible(BoundingBox box) {
                return true;
            }
        }

        private static CameraOrbit CreateCamera(IRenderableObject node) {
            return new SpecialCamera(MathF.ToRadians(32f)) {
                Alpha = 0.9f,
                Beta = 0.1f,
                MinBeta = -0.1f,
                MinY = 0.05f,
                NearZ = 0.1f,
                FarZ = 300f,
                Radius = node?.BoundingBox?.GetSize().Length() * 1.2f ?? 4.8f,
                Target = (node?.BoundingBox?.GetCenter() ?? Vector3.Zero)
                        //- new Vector3(0f, 0.05f, 0f)
                        - new Vector3(0f, 0.25f, 0f)
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

        protected Vector3 ReflectionCubemapPosition => CarNode?.BoundingBox?.GetCenter() ?? Vector3.Zero;

        private Vector3? _previousShadowsTarget;

        protected override void DrawPrepare() {
            base.DrawPrepare();

            var effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();

            var center = ReflectionCubemapPosition;
            if (EffectDarkMaterial.EnableShadows && _previousShadowsTarget != center) {
                _previousShadowsTarget = center;
                _shadows.Update(-_light, center);
                _shadows.DrawScene(DeviceContextHolder, this);

                effect.FxShadowMaps.SetResourceArray(_shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.View).ToArray());
                effect.FxShadowViewProj.SetMatrixArray(
                        _shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.ShadowTransform).ToArray());
            }

            if (CubemapReflection && _reflectionCubemap.Update(center)) {
                _reflectionCubemap.DrawScene(DeviceContextHolder, this);
                effect.FxReflectionCubemap.SetResource(_reflectionCubemap.View);
            }

            effect.FxEyePosW.Set(ActualCamera.Position);
            effect.FxLightDir.Set(_light);
        }

        private TextBlockRenderer _textBlock;

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString($@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
FXAA: {(UseFxaa ? "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}".Trim(),
                    new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                    CoordinateType.Absolute);
        }

        private bool _setCameraHigher = true;

        public bool SetCameraHigher {
            get { return _setCameraHigher; }
            set {
                if (Equals(value, _setCameraHigher)) return;
                _setCameraHigher = value;
                OnPropertyChanged();
            }
        }

        private float _elapsedCamera;
        private float _speed = 1f;

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
                _speed += ((PauseRotation ? 0f : 1f) - _speed) / 5f;

                CameraOrbit.Alpha += _speed * dt * 0.29f;
                CameraOrbit.Beta += _speed * (MathF.Sin(_elapsedCamera * 0.39f) * 0.12f + 0.1f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;

                IsDirty = true;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = _resetCamera.Target + new Vector3(-0.2f * CameraOrbit.Position.X, (SetCameraHigher ? 0f : 0.2f) - 0.1f * CameraOrbit.Position.Y, 0f);
                CameraOrbit.Target += (t - CameraOrbit.Target) / 2f;
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _textBlock);
            DisposeHelper.Dispose(ref _shadows);
            DisposeHelper.Dispose(ref _reflectionCubemap);
            _previousCars.SelectMany(x => x.Objects).DisposeEverything();
            _previousCars.Clear();
            base.Dispose();
        }
    }
}