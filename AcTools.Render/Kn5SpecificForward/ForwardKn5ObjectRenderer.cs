using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
                    Camera = new FpsCamera(orbit.FovY);
                    Camera.LookAt(orbit.Position, orbit.Target, orbit.Up);
                    PrepareCamera(Camera);
                } else {
                    Camera = _resetCamera.Clone();
                    PrepareCamera(Camera);
                }

                Camera.SetLens(AspectRatio);
                IsDirty = true;
            }
        }

        private Vector3 _cameraTo, _showroomOffset;

        public void SetCamera(Vector3 from, Vector3 to, float fovRadY) {
            UseFpsCamera = true;
            Camera = new FpsCamera(fovRadY);

            _cameraTo = to;
            Camera.LookAt(from, to, Vector3.UnitY);
            Camera.SetLens(AspectRatio);
            //Camera.UpdateViewMatrix();

            PrepareCamera(Camera);
            IsDirty = true;
        }

        public Vector3 CarCenter {
            get {
                var result = CarNode?.BoundingBox?.GetCenter() ?? Vector3.Zero;
                result.Y = 0f;
                return result;
            }
        }

        public void AlignCar() {
            var camera = Camera as FpsCamera;
            if (camera == null) return;

            var offset = CarCenter;
            camera.LookAt(camera.Position + offset, _cameraTo += offset, Vector3.UnitY);
            camera.SetLens(AspectRatio);

            offset.Y = 0;
            _showroomOffset = offset;
            if (ShowroomNode != null) {
                ShowroomNode.LocalMatrix = Matrix.Translation(_showroomOffset);
            }

            IsDirty = true;
        }

        public void AlignCamera(bool x, float xOffset, bool xOffsetRelative, bool y, float yOffset, bool yOffsetRelative) {
            if (!x && !y) return;

            var camera = Camera;
            if (camera == null) return;
            
            camera.SetLens(AspectRatio);
            camera.UpdateViewMatrix();

            var offset = GetCameraOffsetForCenterAlignmentUsingVertices(camera, x, xOffset, xOffsetRelative, y, yOffset, yOffsetRelative);
            camera.LookAt(camera.Position + offset, _cameraTo += offset, Vector3.UnitY);

            offset.Y = 0;
            _showroomOffset += offset;
            if (ShowroomNode != null) {
                ShowroomNode.LocalMatrix = Matrix.Translation(_showroomOffset);
            }

            IsDirty = true;
        }

        protected virtual void PrepareCamera(BaseCamera camera) { }

        public bool AsyncTexturesLoading { get; set; } = true;

        public bool AsyncOverridesLoading { get; set; } = false;

        public bool AllowSkinnedObjects { get; set; } = false;

        [CanBeNull]
        public Kn5 Kn5 => CarNode?.OriginalFile;

        private CarDescription _car;
        private readonly string _showroomKn5Filename;

        public ForwardKn5ObjectRenderer(CarDescription car, string showroomKn5Filename = null) {
            _car = car;
            _showroomKn5Filename = showroomKn5Filename;
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
            if (CarNode == null) {
                _selectSkinLater = true;
                _selectSkin = skinId;
            } else {
                CarNode?.SelectSkin(DeviceContextHolder, skinId);
            }
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
            newCar.HeadlightsEnabled = oldCar?.HeadlightsEnabled ?? CarLightsEnabled;
            newCar.BrakeLightsEnabled = oldCar?.BrakeLightsEnabled ?? CarBrakeLightsEnabled;
            newCar.LeftDoorOpen = oldCar?.LeftDoorOpen ?? false;
            newCar.RightDoorOpen = oldCar?.RightDoorOpen ?? false;
            newCar.SteerDeg = oldCar?.SteerDeg ?? 0f;

            if (oldCar != null) {
                oldCar.CamerasChanged -= OnCamerasChanged;
                oldCar.ExtraCamerasChanged -= OnExtraCamerasChanged;
            }

            newCar.CamerasChanged += OnCamerasChanged;
            newCar.ExtraCamerasChanged += OnExtraCamerasChanged;
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
                    _carBoundingBox = null;
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
                    _carBoundingBox = null;

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
                        asyncTexturesLoading: AsyncTexturesLoading, asyncOverrideTexturesLoading: AsyncOverridesLoading,
                        allowSkinnedObjects: AllowSkinnedObjects);
                _selectSkinLater = false;
                CopyValues(loaded, CarNode);

                ClearExisting();

                _carWrapper.Add(loaded);
                ExtendCar(loaded, _carWrapper);

                _car = car;
                _selectSkin = null;
                CarNode = loaded;
                _carBoundingBox = null;

                IsDirty = true;
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
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try {
                _loadingCar = car;

                if (_carWrapper == null) {
                    _car = car;
                    return;
                }

                if (car == null) {
                    ClearExisting();
                    CarNode = null;
                    _carBoundingBox = null;
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
                    _carBoundingBox = null;

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
                            asyncTexturesLoading: AsyncTexturesLoading, asyncOverrideTexturesLoading: AsyncOverridesLoading,
                            allowSkinnedObjects: AllowSkinnedObjects);
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
                _carBoundingBox = null;

                IsDirty = true;
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

        private Kn5RenderableCar _carNode;

        [CanBeNull]
        public Kn5RenderableCar CarNode {
            get { return _carNode; }
            private set {
                if (Equals(value, _carNode)) return;
                _carNode = value;
                IsDirty = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CarCenter));
                OnPropertyChanged(nameof(Kn5));
                OnPropertyChanged(nameof(SelectedLod));
                OnPropertyChanged(nameof(LodsCount));
                OnPropertyChanged(nameof(CarLightsEnabled));
                OnPropertyChanged(nameof(CarBrakeLightsEnabled));
            }
        }

        private Kn5RenderableFile _showroomNode;

        [CanBeNull]
        public Kn5RenderableFile ShowroomNode {
            get { return _showroomNode; }
            private set {
                if (Equals(value, _showroomNode)) return;
                _showroomNode = value;
                IsDirty = true;
                SetReflectionCubemapDirty();
                SetShadowsDirty();
                OnPropertyChanged();
            }
        }

        [ContractAnnotation("showroomKn5:null => null; showroomKn5:notnull => notnull")]
        private Kn5RenderableFile LoadShowroom(string showroomKn5) {
            if (showroomKn5 != null) {
                //var kn5 = Kn5.FromFile(showroomKn5);
                //node = new Kn5RenderableFile(kn5, Matrix.Identity, AsyncTexturesLoading, AllowSkinnedObjects);
                return Kn5RenderableShowroom.Load(Device, showroomKn5, Matrix.Identity, AllowSkinnedObjects);
            }

            return null;
        }

        public void SetShowroom(string showroomKn5) {
            // TODO: errors handling!

            try {
                if (ShowroomNode != null) {
                    ShowroomNode.Dispose();
                    Scene.Remove(ShowroomNode);
                    ShowroomNode = null;
                    GC.Collect();
                }

                var node = LoadShowroom(showroomKn5);
                ShowroomNode = node;

                if (node != null) {
                    Scene.Insert(0, node);
                }
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            } finally {
                CubemapReflection = ShowroomNode != null;
                Scene.UpdateBoundingBox();
                IsDirty = true;
                _sceneDirty = true;
            }
        }

        private bool _cubemapReflection = false;

        public bool CubemapReflection {
            get { return _cubemapReflection; }
            set {
                if (value == _cubemapReflection) return;
                _cubemapReflection = value;

                if (value) {
                    _reflectionCubemap = CreateReflectionCubemap();
                    if (Initialized) {
                        _reflectionCubemap?.Initialize(DeviceContextHolder);
                    }
                } else {
                    DisposeHelper.Dispose(ref _reflectionCubemap);
                }

                IsDirty = true;
                OnPropertyChanged();
                OnCubemapReflectionChanged();
            }
        }
        
        protected virtual void OnCubemapReflectionChanged() {}

        private bool _enableShadows = false;

        public bool EnableShadows {
            get { return _enableShadows; }
            set {
                if (value == _enableShadows) return;
                _enableShadows = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _usePcss = false;

        public bool UsePcss {
            get { return _usePcss; }
            set {
                if (value == _usePcss) return;
                _usePcss = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private ReflectionCubemap _reflectionCubemap;

        [CanBeNull]
        private ShadowsDirectional _shadows;

        protected virtual IMaterialsFactory GetMaterialsFactory() {
            return new MaterialsProviderSimple();
        }

        protected override void DrawScene() {
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            var carNode = CarNode;

            // draw a scene, apart from car
            if (ShowroomNode != null) {
                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
            }

            // draw car
            if (carNode == null) return;

            // shadows
            carNode.DrawAmbientShadows(DeviceContextHolder, ActualCamera);

            // car itself
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            carNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            carNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
        }

        protected virtual void ExtendCar(Kn5RenderableCar car, RenderableList carWrapper) { }

        protected override void InitializeInner() {
            base.InitializeInner();

            DeviceContextHolder.Set(GetMaterialsFactory());

            if (_showroomKn5Filename != null) {
                ShowroomNode = LoadShowroom(_showroomKn5Filename);
                Scene.Insert(0, ShowroomNode);
            }

            _carWrapper = new RenderableList();
            Scene.Add(_carWrapper);

            if (_car != null) {
                var carNode = new Kn5RenderableCar(_car, Matrix.Identity, _selectSkinLater ? _selectSkin : Kn5RenderableCar.DefaultSkin,
                        asyncTexturesLoading: AsyncTexturesLoading, asyncOverrideTexturesLoading: AsyncOverridesLoading,
                        allowSkinnedObjects: AllowSkinnedObjects);
                CarNode = carNode;
                CopyValues(carNode, null);

                _selectSkinLater = false;
                _carWrapper.Add(carNode);
                _carBoundingBox = null;

                ExtendCar(CarNode, _carWrapper);
            }

            // Scene.Add(new Kn5RenderableFile(Kn5.FromFile(_carKn5), Matrix.Identity));

            Scene.UpdateBoundingBox();

            _reflectionCubemap?.Initialize(DeviceContextHolder);

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
            DeviceContextHolder.TexturesUpdated += OnTexturesUpdated;
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
            return new CameraOrbit(32f.ToRadians()) {
                Alpha = 0.9f,
                Beta = 0.1f,
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
            get { return CarNode?.HeadlightsEnabled == true; }
            set {
                if (CarNode != null) {
                    CarNode.HeadlightsEnabled = value;
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

        private bool _reflectionCubemapAtCamera;

        public bool ReflectionCubemapAtCamera {
            get { return _reflectionCubemapAtCamera; }
            set {
                if (Equals(value, _reflectionCubemapAtCamera)) return;
                _reflectionCubemapAtCamera = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected Vector3 ShadowsPosition => GetCarBoundingBox()?.GetCenter() ?? Vector3.Zero;

        protected Vector3 ReflectionCubemapPosition {
            get {
                if (ReflectionCubemapAtCamera) return Camera.Position;

                var b = GetCarBoundingBox();
                return b == null ? Vector3.Zero : b.Value.GetCenter() + Vector3.UnitY * b.Value.GetSize().Y * 0.3f;
            }
        }

        private Vector3? _previousShadowsTarget;

        private Vector3 _light = Vector3.Normalize(new Vector3(-0.2f, 1.0f, 0.8f));

        public Vector3 Light {
            get { return _light; }
            set {
                value = Vector3.Normalize(value);
                if (Equals(_light, value)) return;

                _light = value;
                _sceneDirty = true;
                _reflectionCubemapDirty = true;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _sceneDirty, _sceneWasDirty, _reflectionCubemapDirty, _shadowsEnabled;

        public void SetShadowsDirty() {
            _previousShadowsTarget = null;
        }

        public void SetReflectionCubemapDirty() {
            _reflectionCubemapDirty = true;
        }

        private void OnSceneUpdated(object sender, EventArgs e) {
            _sceneDirty = true;
        }

        private void OnTexturesUpdated(object sender, EventArgs e) {
            SetReflectionCubemapDirty();
        }

        protected virtual void UpdateShadows(ShadowsDirectional shadows, Vector3 center) {
            _previousShadowsTarget = center;

            if (!EnableShadows) {
                shadows.Clear(DeviceContextHolder);
            } else {
                shadows.Update(-Light, center);
                shadows.DrawScene(DeviceContextHolder, this);
            }
        }

        public bool DelayedBoundingBoxUpdate { get; set; }

        protected void DrawPrepare(Vector3 eyesPosition, Vector3 light) {
            var sceneDirty = _sceneDirty;
            _sceneDirty = false;

            if (sceneDirty) {
                if (!DelayedBoundingBoxUpdate) {
                    Scene.UpdateBoundingBox();
                }

                _sceneWasDirty = true;
            } else {
                if (_sceneWasDirty && DelayedBoundingBoxUpdate) {
                    Scene.UpdateBoundingBox();
                }

                _sceneWasDirty = false;
            }

            var shadowsPosition = ShadowsPosition;
            if (_shadows != null && (_previousShadowsTarget != shadowsPosition || sceneDirty || _shadowsEnabled != EnableShadows)) {
                UpdateShadows(_shadows, shadowsPosition);
                _shadowsEnabled = EnableShadows;
            }

            var reflectionPosition = ReflectionCubemapPosition;
            if (_reflectionCubemap != null && (_reflectionCubemap.Update(reflectionPosition) || _reflectionCubemapDirty)) {
                _reflectionCubemap.BackgroundColor = (Color4)BackgroundColor * BackgroundBrightness;
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

        protected virtual string GetInformationString() {
            return $@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
Triangles: {CarNode?.TrianglesCount:D}
FXAA: {(UseFxaa ? "Yes" : "No")}
MSAA: {(UseMsaa ? "Yes" : "No")}
SSAA: {(UseSsaa ? TemporaryFlag ? "Yes, Exp." : "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}
Magick.NET: {(ImageUtils.IsMagickSupported ? "Yes" : "No")}".Trim();
        }

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString(GetInformationString(),
                    new Vector2(ActualWidth - 300, 20), 16f, UiColor,
                    CoordinateType.Absolute);

            if (CarNode == null) return;

            var offset = 15;
            if (CarNode.LodsCount > 0) {
                var information = CarNode.CurrentLodInformation;
                _textBlock.DrawString(
                        $"LOD #{CarNode.CurrentLod + 1} ({CarNode.LodsCount} in total; shown from {information?.In.ToInvariantString() ?? "?"} to {information?.Out.ToInvariantString() ?? "?"})",
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
        
        private BoundingBox? _carBoundingBox;

        protected BoundingBox? GetCarBoundingBox() {
            return _carBoundingBox ?? (_carBoundingBox = CarNode?.BoundingBox);
        }

        private void GetCameraOffsetForCenterAlignmentUsingVertices_ProcessObject(Kn5RenderableObject obj, Matrix matrix, ref Vector3 min, ref Vector3 max) {
            if (min.Z != 0f && obj.BoundingBox.HasValue) {
                var corners = obj.BoundingBox.Value.GetCorners();
                foreach (var c in corners) {
                    var sp = Vector3.TransformCoordinate(c, matrix);
                    if (sp.X < min.X || sp.Y < min.Y || sp.Z < min.Z ||
                            sp.X > max.X || sp.Y > max.Y || sp.Z > max.Z) {
                        goto NextStep;
                    }
                }

                return;
            }

            NextStep:
            var wm = obj.ParentMatrix * matrix;
            var vertices = obj.Vertices;
            for (var i = 0; i < vertices.Length; i++) {
                var sp = Vector3.TransformCoordinate(vertices[i].Position, wm);
                if (min.Z == 0f) {
                    min = sp;
                    max = sp;
                } else {
                    if (sp.X < min.X) min.X = sp.X;
                    if (sp.Y < min.Y) min.Y = sp.Y;
                    if (sp.Z < min.Z) min.Z = sp.Z;
                    if (sp.X > max.X) max.X = sp.X;
                    if (sp.Y > max.Y) max.Y = sp.Y;
                    if (sp.Z > max.Z) max.Z = sp.Z;
                }
            }
        }

        private void GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(RenderableList list, Matrix matrix, ref Vector3 min, ref Vector3 max) {
            for (var i = 0; i < list.Count; i++) {
                var child = list[i];
                var li = child as RenderableList;
                if (li != null) {
                    GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(li, matrix, ref min, ref max);
                } else {
                    var ro = child as Kn5RenderableObject;
                    if (ro != null) {
                        GetCameraOffsetForCenterAlignmentUsingVertices_ProcessObject(ro, matrix, ref min, ref max);
                    }
                }
            }
        }

        private Vector3 GetCameraOffsetForCenterAlignmentUsingVertices(ICamera camera, bool x, float xOffset, bool xOffsetRelative, bool y, float yOffset, bool yOffsetRelative) {
            if (CarNode == null) return Vector3.Zero;

            var matrix = camera.ViewProj;
            var min = Vector3.Zero;
            var max = Vector3.Zero;
            GetCameraOffsetForCenterAlignmentUsingVertices_ProcessList(CarNode.RootObject, matrix, ref min, ref max);

            var center = (min + max) / 2f;
            var offsetScreen = center;
            var targetScreen = new Vector3(xOffset, yOffset, offsetScreen.Z);

            if (!x) {
                targetScreen.X = offsetScreen.X;
            } else if (xOffsetRelative) {
                if (xOffset > 0) {
                    var left = (0.5f - max.X / 2f).Saturate();
                    targetScreen.X = xOffset * left;
                } else {
                    var left = (min.X / 2f + 0.5f).Saturate();
                    targetScreen.X = xOffset * left;
                }
            }

            if (!y) {
                targetScreen.Y = offsetScreen.Y;
            } else if (yOffsetRelative) {
                if (yOffset > 0) {
                    var left = (0.5f - max.Y / 2f).Saturate();
                    targetScreen.Y = yOffset * left;
                } else {
                    var left = (min.Y / 2f + 0.5f).Saturate();
                    targetScreen.Y = yOffset * left;
                }
            }

            return Vector3.TransformCoordinate(offsetScreen, camera.ViewProjInvert) -
                    Vector3.TransformCoordinate(targetScreen, camera.ViewProjInvert);
        }

        private Vector3 GetCameraOffsetForCenterAlignment(ICamera camera, bool limited) {
            var nbox = GetCarBoundingBox();
            if (nbox == null) return Vector3.Zero;

            var box = nbox.Value;
            var corners = box.GetCorners();

            if (camera.Position.X >= box.Minimum.X &&
                    camera.Position.Y >= box.Minimum.X &&
                    camera.Position.Z >= box.Minimum.X &&
                    box.Maximum.X >= camera.Position.X &&
                    box.Maximum.Y >= camera.Position.X &&
                    box.Maximum.Z >= camera.Position.X) {
                return Vector3.Zero;
            }

            return GetCameraOffsetForCenterAlignment(corners, camera, limited);
        }

        private static Vector3 GetCameraOffsetForCenterAlignment(Vector3[] corners, ICamera camera, bool limited) {
            var center = Vector3.Zero;
            for (var i = 0; i < corners.Length; i++) {
                var vec = Vector3.TransformCoordinate(corners[i], camera.ViewProj);
                if (limited && new Vector2(vec.X, vec.Y).Length() > 15f) return Vector3.Zero;
                center += vec;
            }

            var offsetScreen = center / corners.Length;
            return Vector3.TransformCoordinate(offsetScreen, camera.ViewProjInvert) -
                    Vector3.TransformCoordinate(new Vector3(0f, 0f, offsetScreen.Z), camera.ViewProjInvert);
        }

        private static float GetMaxCornerOffset(CameraOrbit camera) {
            camera.UpdateViewMatrix();

            var test = camera.Target + camera.Right * camera.Radius * 0.01f;
            return Vector3.TransformCoordinate(test, camera.ViewProj).X.Abs();
        }

        private static float GetMaxCornerOffset(Vector3[] corners, CameraOrbit camera) {
            camera.UpdateViewMatrix();

            var maxOffset = 0f;
            for (var i = 0; i < corners.Length; i++) {
                var vec = Vector3.TransformCoordinate(corners[i], camera.ViewProj);
                var offset = new Vector2(vec.X, vec.Y).Length();
                maxOffset += offset;
            }

            return maxOffset / corners.Length;
        }

        public void ChangeCameraFov(float newFovY) {
            var c = CameraOrbit;
            if (c == null) return;

            var offset = GetMaxCornerOffset(c);
            
            c.FovY = newFovY.Clamp(MathF.PI * 0.01f, MathF.PI * 0.8f);
            c.SetLens(c.Aspect);

            var newOffset = GetMaxCornerOffset(c);

            c.Radius *= newOffset / offset;

            if (AutoAdjustTarget) {
                c.Target = AutoAdjustedTarget;
            }
        }

        protected virtual Vector3 AutoAdjustedTarget {
            get {
                var camera = CameraOrbit;
                if (camera == null) return Vector3.Zero;

                camera.UpdateViewMatrix();
                return camera.Target + GetCameraOffsetForCenterAlignment(camera, true);
            }
        }

        private float _animationsMultipler = 1f;

        public float AnimationsMultipler {
            get { return _animationsMultipler; }
            set {
                if (Equals(value, _animationsMultipler)) return;
                _animationsMultipler = value;
                OnPropertyChanged();
            }
        }

        private float _elapsedCamera;

        protected override void OnTick(float dt) {
            base.OnTick(dt);

            CarNode?.OnTick(dt * AnimationsMultipler);

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
                    cam.SetLens(cam.Aspect);
                }

                _elapsedCamera = 0f;

                IsDirty = true;
            } else if (AutoRotate && CameraOrbit != null) {
                CameraOrbit.Alpha -= dt * 0.29f;
                CameraOrbit.Beta += ((_elapsedCamera * 0.39f).Sin() * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;

                IsDirty = true;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = AutoAdjustedTarget;
                CameraOrbit.Target += (t - CameraOrbit.Target) / 3f;
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

        public enum CarCameraMode {
            None, FirstPerson, Dashboard, Bonnet, Bumper
        }

        [CanBeNull]
        private BaseCamera GetCamera(CarCameraMode mode) {
            switch (mode) {
                case CarCameraMode.None:
                    return null;
                case CarCameraMode.FirstPerson:
                    return CarNode?.GetDriverCamera();
                case CarCameraMode.Dashboard:
                    return CarNode?.GetDashboardCamera();
                case CarCameraMode.Bonnet:
                    return CarNode?.GetBonnetCamera();
                case CarCameraMode.Bumper:
                    return CarNode?.GetBumperCamera();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void SwitchCamera(CarCameraMode mode) {
            var camera = GetCamera(mode);
            if (camera == null) {
                UseFpsCamera = false;
                return;
            }

            UseFpsCamera = true;
            Camera = camera;
            Camera.SetLens(AspectRatio);
            PrepareCamera(Camera);
        }

        private void OnCamerasChanged(object sender, EventArgs e) {
            if (CurrentMode != CarCameraMode.None) {
                SwitchCamera(CurrentMode);
            }
        }

        private void OnExtraCamerasChanged(object sender, EventArgs e) {
            if (CurrentExtraCamera.HasValue) {
                SwitchCamera(CurrentExtraCamera);
            }
        }

        private CarCameraMode _currentMode;

        public CarCameraMode CurrentMode {
            get { return _currentMode; }
            set {
                if (Equals(value, _currentMode)) return;
                _currentMode = value;
                _currentExtraCamera = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentExtraCamera));
                SwitchCamera(value);
            }
        }

        public void NextCamera() {
            CurrentMode = CurrentMode.NextValue();
        }

        private void SwitchCamera(int? cameraId) {
            var camera = cameraId == null ? null : CarNode?.GetCamera(cameraId.Value);
            if (camera == null) {
                UseFpsCamera = false;
                return;
            }

            UseFpsCamera = true;
            Camera = camera;
            Camera.SetLens(AspectRatio);
            PrepareCamera(Camera);
        }

        private int? _currentExtraCamera;

        public int? CurrentExtraCamera {
            get { return _currentExtraCamera; }
            set {
                if (value == _currentExtraCamera) return;
                _currentExtraCamera = value;
                _currentMode = CarCameraMode.None;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentMode));
                SwitchCamera(value);
            }
        }

        public void NextExtraCamera() {
            var cameras = CarNode?.GetCamerasCount();
            if (!cameras.HasValue || cameras == 0) {
                CurrentExtraCamera = null;
            } else {
                CurrentExtraCamera = CurrentExtraCamera.HasValue ? (CurrentExtraCamera.Value + 1) % cameras : 0;
            }
        }
    }
}
