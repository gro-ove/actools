using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Sprites;
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
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificForward {
    public class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        public bool AutoRotate { get; set; } = true;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool VisibleUi { get; set; } = true;

        public Color UiColor { get; set; } = Color.White;

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
                    PrepareCamera(Camera);
                } else {
                    Camera = _resetCamera.Clone();
                    PrepareCamera(Camera);
                }

                Camera.SetLens(AspectRatio);
            }
        }

        public void SetCamera(Vector3 from, Vector3 to, float fovY) {
            var orbit = CameraOrbit ?? CreateCamera(Scene);

            Camera = new FpsCamera(fovY) {
                NearZ = orbit.NearZ,
                FarZ = orbit.FarZ
            };

            Camera.LookAt(from, to, Vector3.UnitY);
            PrepareCamera(Camera);
        }

        protected virtual void PrepareCamera(BaseCamera camera) { }

        public bool AsyncTexturesLoading { get; set; } = true;

        public bool AllowSkinnedObjects { get; set; } = false;

        public Kn5 Kn5 { get; private set; }
        private CarDescription _car;
        private readonly string _showroomKn5Filename;

        public ForwardKn5ObjectRenderer(CarDescription car, string showroomKn5Filename = null) {
            _car = car;
            _showroomKn5Filename = showroomKn5Filename;
            Kn5 = Kn5.FromFile(_car.MainKn5File);
        }

        public int CacheSize { get; } = 0;
        
        private bool _selectSkinLater;
        private string _selectSkin = Kn5RenderableCar.DefaultSkin;

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

        private RenderableList _carWrapper;
        private CarDescription _loadingCar;

        private class PreviousCar {
            public string Id;
            public List<IRenderableObject> Objects;
        }

        private readonly List<PreviousCar> _previousCars = new List<PreviousCar>(2);

        private void ClearExisting() {
            if (_car != null && CacheSize > 0) {
                var existing = _previousCars.FirstOrDefault(x => x.Id == _car.MainKn5File);
                if (existing != null) {
                    _previousCars.Remove(existing);
                    _previousCars.Add(existing);
                } else if (_carWrapper.OfType<Kn5RenderableCar>().Any()) {
                    if (_previousCars.Count >= CacheSize) {
                        var toRemoval = _previousCars[0];
                        toRemoval.Objects.DisposeEverything();
                        _previousCars.RemoveAt(0);
                    }

                    _previousCars.Add(new PreviousCar {
                        Id = _car.MainKn5File,
                        Objects = _carWrapper.ToList()
                    });

                    _carWrapper.Clear();
                    return;
                }
            }

            _carWrapper.DisposeEverything();
        }

        protected virtual void ClearBeforeChangingCar() { }

        private void CopyValues([NotNull] Kn5RenderableCar newCar, [CanBeNull] Kn5RenderableCar oldCar) {
            newCar.LightsEnabled = oldCar?.LightsEnabled ?? CarLightsEnabled;
            newCar.BrakeLightsEnabled = oldCar?.BrakeLightsEnabled ?? CarBrakeLightsEnabled;
            newCar.SteerDeg = oldCar?.SteerDeg ?? 0f;
        }

        public void SetCar(CarDescription car, string skinId = Kn5RenderableCar.DefaultSkin) {
            ClearBeforeChangingCar();

            try {
                _loadingCar = car;

                if (_carWrapper == null) {
                    _car = car;
                    return;
                }
                
                if (car == null) {
                    ClearExisting();
                    CarNode = null;
                    _car = null;
                    Scene.UpdateBoundingBox();
                    return;
                }

                Kn5RenderableCar loaded;

                var previous = _previousCars.FirstOrDefault(x => x.Id == car.MainKn5File);
                if (previous != null) {
                    _previousCars.Remove(previous);

                    ClearExisting();
                    _carWrapper.AddRange(previous.Objects);
                    _car = car;
                    loaded = previous.Objects.OfType<Kn5RenderableCar>().First();
                    CopyValues(loaded, CarNode);
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
                
                loaded = new Kn5RenderableCar(car, Matrix.Identity, _selectSkinLater ? _selectSkin : skinId,
                        asyncTexturesLoading: AsyncTexturesLoading, allowSkinnedObjects: AllowSkinnedObjects);
                _selectSkinLater = false;
                CopyValues(loaded, CarNode);

                ClearExisting();

                _carWrapper.Add(loaded);
                ExtendCar(loaded, _carWrapper);

                _car = car;
                _selectSkin = null;
                CarNode = loaded;
                Scene.UpdateBoundingBox();
            } catch (Exception e) {
                MessageBox.Show(e.ToString());
                throw;
            } finally {
                if (ReferenceEquals(_loadingCar, car)) {
                    _loadingCar = null;
                }
            }
        }

        public async Task SetCarAsync(CarDescription car, string skinId = Kn5RenderableCar.DefaultSkin,
                CancellationToken cancellationToken = default(CancellationToken)) {
            ClearBeforeChangingCar();

            try {
                _loadingCar = car;

                if (_carWrapper == null) {
                    _car = car;
                    return;
                }

                if (car == null) {
                    ClearExisting();
                    CarNode = null;
                    _car = null;
                    Scene.UpdateBoundingBox();
                    return;
                }

                Kn5RenderableCar loaded = null;

                var previous = _previousCars.FirstOrDefault(x => x.Id == car.MainKn5File);
                if (previous != null) {
                    _previousCars.Remove(previous);

                    ClearExisting();
                    _carWrapper.AddRange(previous.Objects);
                    _car = car;
                    loaded = previous.Objects.OfType<Kn5RenderableCar>().First();
                    CopyValues(loaded, CarNode);
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

                await car.LoadAsync();
                if (cancellationToken.IsCancellationRequested) return;

                await Task.Run(() => {
                    loaded = new Kn5RenderableCar(car, Matrix.Identity, _selectSkinLater ? _selectSkin : skinId,
                            asyncTexturesLoading: AsyncTexturesLoading);
                    _selectSkinLater = false;
                    if (cancellationToken.IsCancellationRequested) return;

                    CopyValues(loaded, CarNode);
                    if (cancellationToken.IsCancellationRequested) return;

                    loaded.Draw(DeviceContextHolder, null, SpecialRenderMode.InitializeOnly);
                });

                if (cancellationToken.IsCancellationRequested || _loadingCar != car) {
                    loaded?.Dispose();
                    return;
                }

                ClearExisting();

                _carWrapper.Add(loaded);
                ExtendCar(loaded, _carWrapper);

                _car = car;
                _selectSkin = null;
                CarNode = loaded;
                Scene.UpdateBoundingBox();
            } catch (Exception e) {
                MessageBox.Show(e.ToString());
                throw;
            } finally {
                if (ReferenceEquals(_loadingCar, car)) {
                    _loadingCar = null;
                }
            }
        }

        [CanBeNull]
        public Kn5RenderableCar CarNode { get; private set; }

        public bool PauseRotation { get; set; } = false;

        public bool CubemapReflection { get; set; } = false;

        public bool EnableShadows { get; set; } = false;

        [CanBeNull]
        private ReflectionCubemap _reflectionCubemap;

        [CanBeNull]
        private ShadowsDirectional _shadows;

        protected virtual IMaterialsFactory GetMaterialsFactory() {
            return new MaterialsProviderSimple();
        }

        protected virtual void ExtendCar(Kn5RenderableCar car, RenderableList carWrapper) { }

        protected override void InitializeInner() {
            base.InitializeInner();

            DeviceContextHolder.Set(GetMaterialsFactory());

            if (_showroomKn5Filename != null) {
                var kn5 = Kn5.FromFile(_showroomKn5Filename);
                Scene.Insert(0, new Kn5RenderableFile(kn5, Matrix.Identity));
            }

            _carWrapper = new RenderableList();
            Scene.Add(_carWrapper);

            if (_car != null) {
                CarNode = new Kn5RenderableCar(_car, Matrix.Identity, _selectSkinLater ? _selectSkin : Kn5RenderableCar.DefaultSkin,
                        asyncTexturesLoading: AsyncTexturesLoading, allowSkinnedObjects: AllowSkinnedObjects);
                CopyValues(CarNode, null);

                _selectSkinLater = false;
                _carWrapper.Add(CarNode);

                ExtendCar(CarNode, _carWrapper);
            }

            // Scene.Add(new Kn5RenderableFile(Kn5.FromFile(_carKn5), Matrix.Identity));

            Scene.UpdateBoundingBox();

            if (CubemapReflection) {
                _reflectionCubemap = CreateReflectionCubemap();
                _reflectionCubemap?.Initialize(DeviceContextHolder);
            }

            if (EnableShadows) {
                _shadows = CreateShadows();
                _shadows?.Initialize(DeviceContextHolder);
            }

            if (Camera == null) {
                Camera = CreateCamera(CarNode);
                _resetCamera = (CameraOrbit)Camera.Clone();
                PrepareCamera(Camera);
            }

            DeviceContextHolder.SceneUpdated += OnSceneUpdated;
        }

        [CanBeNull]
        protected virtual ReflectionCubemap CreateReflectionCubemap() {
            return null;
        }

        [CanBeNull]
        protected virtual ShadowsDirectional CreateShadows() {
            return null;
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

        public bool CarBrakeLightsEnabled {
            get { return CarNode?.BrakeLightsEnabled == true; }
            set {
                if (CarNode != null) {
                    CarNode.BrakeLightsEnabled = value;
                }
            }
        }

        protected virtual Vector3 ReflectionCubemapPosition => CarNode?.BoundingBox?.GetCenter() ?? Vector3.Zero;

        private Vector3? _previousShadowsTarget;

        private Vector3 _light = Vector3.Normalize(new Vector3(0.2f, 1.0f, 0.8f));

        private Vector3 Light {
            get { return _light; }
            set {
                value = Vector3.Normalize(value);
                if (Equals(_light, value)) return;

                _light = value;
                _shadowsDirty = true;
            }
        }

        private bool _shadowsDirty, _reflectionCubemapDirty;

        private void OnSceneUpdated(object sender, EventArgs e) {
            _shadowsDirty = true;
        }

        protected virtual void DrawPrepare(Vector3 eyesPosition, Vector3 light) {
            var center = ReflectionCubemapPosition;
            if (_shadows != null && (_previousShadowsTarget != center || _shadowsDirty)) {
                _previousShadowsTarget = center;
                _shadows.Update(-Light, center);
                _shadows.DrawScene(DeviceContextHolder, this);
                _shadowsDirty = false;
            }

            if (_reflectionCubemap != null && (_reflectionCubemap.Update(center) || _reflectionCubemapDirty)) {
                _reflectionCubemap.DrawScene(DeviceContextHolder, this);
                _reflectionCubemapDirty = false;
            }

            DrawPrepareEffect(eyesPosition, light, _shadows, _reflectionCubemap);
        }

        protected virtual void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, [CanBeNull] ShadowsDirectional shadows,
                [CanBeNull] ReflectionCubemap reflection) {
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(ActualCamera.Position);
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DrawPrepare(ActualCamera.Position, Light);
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
FXAA: {(UseFxaa && (!UseMsaa || UseSsaa) ? "Yes" : "No")}
MSAA: {(UseMsaa && !UseSsaa ? "Yes" : "No")}
SSAA: {(UseSsaa ? "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}
Magick.NET: {(ImageUtils.IsMagickSupported ? "Yes" : "No")}".Trim(),
                    new Vector2(ActualWidth - 300, 20), 16f, UiColor,
                    CoordinateType.Absolute);

            if (CarNode == null) return;

            var offset = 15;
            if (CarNode.LodsCount > 0) {
                var information = CarNode.CurrentLodInformation;
                _textBlock.DrawString($"LOD #{CarNode.CurrentLod + 1} ({CarNode.LodsCount} in total; shown from {information.In} to {information.Out})",
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, UiColor,
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
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, UiColor,
                        CoordinateType.Absolute);
                offset += 20;
            }

            if (CarNode.Skins != null && CarNode.CurrentSkin != null) {
                _textBlock.DrawString($"{CarNode.CurrentSkin} ({CarNode.Skins.IndexOf(CarNode.CurrentSkin) + 1}/{CarNode.Skins.Count})",
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, UiColor,
                        CoordinateType.Absolute);
            }
        }

        protected virtual Vector3 AutoAdjustedTarget =>
                new Vector3(-0.2f * (CameraOrbit?.Position.X ?? 0f), -0.1f * (CameraOrbit?.Position.Y ?? 0f), 0f);

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
                var t = _resetCamera.Target + AutoAdjustedTarget;
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
