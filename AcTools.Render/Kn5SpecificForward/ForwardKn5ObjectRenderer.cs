using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
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
    public partial class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        private bool _autoRotate;

        public bool AutoRotate {
            get => _autoRotate;
            set {
                if (value == _autoRotate) return;
                _autoRotate = value;
                OnPropertyChanged();
            }
        }

        private float _autoRotateSpeed = 1f;

        public float AutoRotateSpeed {
            get => _autoRotateSpeed;
            set {
                if (Equals(value, _autoRotateSpeed)) return;
                _autoRotateSpeed = value;
                OnPropertyChanged();
            }
        }

        private bool _autoAdjustTarget = true;

        public bool AutoAdjustTarget {
            get => _autoAdjustTarget;
            set {
                if (value == _autoAdjustTarget) return;
                _autoAdjustTarget = value;
                OnPropertyChanged();
            }
        }

        private float _carShadowsOpacity = 1f;

        public float CarShadowsOpacity {
            get => _carShadowsOpacity;
            set {
                if (Equals(value, _carShadowsOpacity)) return;
                _carShadowsOpacity = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        public bool VisibleUi { get; set; } = true;

        public Color UiColor { get; set; } = Color.White;

        private bool _useFpsCamera;

        public bool UseFpsCamera {
            get => _useFpsCamera;
            set {
                if (Equals(value, _useFpsCamera)) return;
                _useFpsCamera = value;
                OnPropertyChanged();

                if (value) {
                    var orbit = CameraOrbit ?? CreateCamera(Scene);
                    Camera = new FpsCamera(orbit.FovY);
                    Camera.LookAt(orbit.Position, orbit.Target, orbit.Tilt);
                    PrepareCamera(Camera);
                } else {
                    Camera = _resetCamera.Clone();
                    PrepareCamera(Camera);
                }

                Camera.SetLens(AspectRatio);
                IsDirty = true;
            }
        }

        protected virtual void PrepareCamera(CameraBase camera) { }

        public bool AsyncTexturesLoading { get; set; } = true;
        public bool AsyncOverridesLoading { get; set; } = false;
        public bool AllowSkinnedObjects { get; set; } = false;

        private readonly string _showroomKn5Filename;

        public ForwardKn5ObjectRenderer(CarDescription car, string showroomKn5Filename = null) {
            _showroomKn5Filename = showroomKn5Filename;

            _mainSlot = new CarSlot(this, car, 0);
            _carSlots = new[] { MainSlot };

            MainSlot.PropertyChanged += OnMainSlotPropertyChanged;
        }

        private void OnMainSlotPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(CarSlot.CarNode):
                case nameof(CarSlot.Kn5):
                    OnPropertyChanged(e.PropertyName);
                    break;
            }
        }

        private CarSlot _mainSlot;

        [NotNull]
        public CarSlot MainSlot {
            get => _mainSlot;
            private set {
                if (Equals(value, _mainSlot)) return;
                _mainSlot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CarLightsEnabled));
                OnPropertyChanged(nameof(CarBrakeLightsEnabled));
                OnPropertyChanged(nameof(CarNode));
            }
        }

        private CarSlot[] _carSlots;

        [NotNull]
        public CarSlot[] CarSlots {
            get => _carSlots;
            set {
                if (value.Length == 0) value = new[] { _mainSlot };
                if (Equals(value, _carSlots)) return;

                if (_carSlots != null) {
                    foreach (var removed in _carSlots.Where(x => !value.Contains(x))) {
                        removed.SetCar(null);
                        OnCarSlotRemoved(removed);
                    }
                }

                _carSlots = value;
                MainSlot = value[0];
                OnPropertyChanged();

                if (Initialized) {
                    SetShadowsDirty();
                    SetReflectionCubemapDirty();
                    IsDirty = true;
                }
            }
        }

        protected virtual void OnCarSlotRemoved(CarSlot slot){}

        public void AddSlot(CarSlot slot) {
            var updated = new CarSlot[_carSlots.Length + 1];
            Array.Copy(_carSlots, updated, _carSlots.Length);
            updated[updated.Length - 1] = slot;
            CarSlots = updated;
        }

        public void InsertSlotAt(CarSlot slot, int index) {
            var updated = new CarSlot[_carSlots.Length + 1];
            Array.Copy(_carSlots, updated, index);
            Array.Copy(_carSlots, index, updated, index + 1, _carSlots.Length - index);
            updated[index] = slot;
            CarSlots = updated;
        }

        public void RemoveSlot(CarSlot slot) {
            var index = _carSlots.IndexOf(slot);
            if (index == -1) return;

            var updated = new CarSlot[_carSlots.Length - 1];
            Array.Copy(_carSlots, updated, index);
            Array.Copy(_carSlots, index + 1, updated, index, updated.Length - index);
            CarSlots = updated;
        }

        public CarSlot AddCar(CarDescription car) {
            var slot = new CarSlot(this, car, null) {
                LocalMatrix = Matrix.Translation(_carSlots.Length * 2.5f, 0f, 0f)
            };

            AddSlot(slot);

            if (Initialized) {
                slot.Initialize();
                Scene.Add(slot.CarWrapper);
                Scene.UpdateBoundingBox();
            }

            return slot;
        }

        protected override void InitializeInner() {
            base.InitializeInner();

            DeviceContextHolder.Set(GetMaterialsFactory());

            if (_showroomKn5Filename != null) {
                ShowroomNode = LoadShowroom(_showroomKn5Filename);
                Scene.Insert(0, ShowroomNode);
            }

            foreach (var carSlot in CarSlots) {
                carSlot.Initialize();
                Scene.Add(carSlot.CarWrapper);
            }

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
        public Kn5RenderableCar CarNode => MainSlot.CarNode;

        private Kn5RenderableShowroom _showroomNode;

        [CanBeNull]
        public Kn5RenderableShowroom ShowroomNode {
            get => _showroomNode;
            private set {
                if (Equals(value, _showroomNode)) return;
                _showroomNode = value;
                OnPropertyChanged();
                OnShowroomChanged();
            }
        }

        protected virtual void OnShowroomChanged() {
            _sceneDirty = true;
            IsCubemapReflectionActive = ShowroomNode != null;
            IsDirty = true;
            SetReflectionCubemapDirty();
            SetShadowsDirty();
        }

        [ContractAnnotation("showroomKn5:null => null; showroomKn5:notnull => notnull")]
        private Kn5RenderableShowroom LoadShowroom(string showroomKn5) {
            return showroomKn5 != null ? Kn5RenderableShowroom.Load(Device, showroomKn5, Matrix.Identity, AllowSkinnedObjects) : null;
        }

        public void SetShowroom(string showroomKn5) {
            try {
                if (ShowroomNode != null) {
                    ShowroomNode.Dispose();
                    Scene.Remove(ShowroomNode);
                    ShowroomNode = null;
                }

                var node = LoadShowroom(showroomKn5);
                ShowroomNode = node;

                if (node != null) {
                    Scene.Insert(0, node);
                }
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Can’t load showroom", null, e);
            } finally {
                Scene.UpdateBoundingBox();
                IsDirty = true;
                _sceneDirty = true;
            }
        }

        private bool _isCubemapReflectionActive;

        public bool IsCubemapReflectionActive {
            get => _isCubemapReflectionActive;
            private set {
                if (value == _isCubemapReflectionActive) return;
                _isCubemapReflectionActive = value;

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

        protected void OnCubemapReflectionChanged() {}

        private bool _enableShadows;

        public bool EnableShadows {
            get => _enableShadows;
            set {
                if (value == _enableShadows) return;
                _enableShadows = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _usePcss;

        public bool UsePcss {
            get => _usePcss;
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

        protected virtual void DrawCars(DeviceContextHolder holder, ICamera camera, SpecialRenderMode mode) {
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                CarSlots[i].CarNode?.Draw(holder, camera, mode);
            }
        }

        protected override void DrawScene() {
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            // draw a scene, apart from car
            if (ShowroomNode != null) {
                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
            }

            // shadows
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                CarSlots[i].CarNode?.DrawAmbientShadows(DeviceContextHolder, ActualCamera);
            }

            // car itself
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
        }

        protected virtual void ExtendCar(CarSlot slot, Kn5RenderableCar car, RenderableList carWrapper) { }

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
            _resetState = 1f;
            IsDirty = true;
        }

        private bool _reflectionCubemapAtCamera;

        public bool ReflectionCubemapAtCamera {
            get => _reflectionCubemapAtCamera;
            set {
                if (Equals(value, _reflectionCubemapAtCamera)) return;
                _reflectionCubemapAtCamera = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected Vector3 ShadowsPosition => MainSlot.GetCarBoundingBox()?.GetCenter() ?? Vector3.Zero;

        protected Vector3 ReflectionCubemapPosition {
            get {
                if (ReflectionCubemapAtCamera) return Camera.Position;

                var b = MainSlot.GetCarBoundingBox();
                return b == null ? Vector3.Zero : b.Value.GetCenter() + Vector3.UnitY * b.Value.GetSize().Y * 0.3f;
            }
        }

        private Vector3? _previousShadowsTarget;

        private Vector3 _light = Vector3.Normalize(new Vector3(-0.2f, 1.0f, 0.8f));

        public Vector3 Light {
            get => _light;
            set {
                value = Vector3.Normalize(value);
                if (Equals(_light, value)) return;

                _light = value;
                _sceneDirty = true;
                IsDirty = true;
                SetReflectionCubemapDirty();
                OnPropertyChanged();
            }
        }

        private bool _sceneDirty, _sceneWasDirty, _shadowsEnabled;

        protected void SetShadowsDirty() {
            _previousShadowsTarget = null;
        }

        protected virtual void SetReflectionCubemapDirty() {
            _reflectionCubemap?.SetDirty();
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

        private int _cubemapReflectionMapSize = 2048;

        public int CubemapReflectionMapSize {
            get => _cubemapReflectionMapSize;
            set {
                if (Equals(value, _cubemapReflectionMapSize)) return;
                _cubemapReflectionMapSize = value;
                _reflectionCubemap?.SetResolution(DeviceContextHolder, value);
                OnPropertyChanged();
            }
        }

        private bool _forceUpdateWholeCubemapAtOnce;

        public bool ForceUpdateWholeCubemapAtOnce {
            get => _forceUpdateWholeCubemapAtOnce;
            set {
                if (Equals(value, _forceUpdateWholeCubemapAtOnce)) return;
                _forceUpdateWholeCubemapAtOnce = value;
                OnPropertyChanged();
            }
        }

        private int _cubemapReflectionFacesPerFrame = 1;

        public int CubemapReflectionFacesPerFrame {
            get => _cubemapReflectionFacesPerFrame;
            set {
                value = value.Clamp(1, 6);
                if (Equals(value, _cubemapReflectionFacesPerFrame)) return;
                _cubemapReflectionFacesPerFrame = value;
                OnPropertyChanged();
            }
        }

        public bool DelayedBoundingBoxUpdate { get; set; }

        protected virtual void DrawPrepare(Vector3 eyesPosition, Vector3 light) {
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
            if (_reflectionCubemap != null) {
                _reflectionCubemap.Update(reflectionPosition);
                _reflectionCubemap.BackgroundColor = (Color4)BackgroundColor * BackgroundBrightness;

                _reflectionCubemap.DrawScene(DeviceContextHolder, this,
                        ShotDrawInProcess || ForceUpdateWholeCubemapAtOnce ? 6 : CubemapReflectionFacesPerFrame);
                if (_reflectionCubemap.IsDirty) {
                    // Finish updating in the next frame
                    IsDirty = true;
                }
            }

            DrawPrepareEffect(eyesPosition, light, _shadows, _reflectionCubemap, false);
        }

        protected virtual void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, [CanBeNull] ShadowsDirectional shadows,
                [CanBeNull] ReflectionCubemap reflection, bool singleLight) {
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
SSAA: {(UseSsaa ? "Yes" : "No")}
Bloom: {(UseBloom ? "Yes" : "No")}
Magick.NET: {(ImageUtils.IsMagickSupported ? "Yes" : "No")}".Trim();
        }

        protected override void DrawSpritesInner() {
            if (!VisibleUi || ShotDrawInProcess) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString(GetInformationString(),
                    new Vector2(ActualWidth - 300, 20), 0f, 16f, UiColor,
                    CoordinateType.Absolute);

            if (CarNode == null) return;

            var offset = 15;
            if (CarNode.LodsCount > 0) {
                var information = CarNode.CurrentLodInformation;
                _textBlock.DrawString(
                        $"LOD #{CarNode.CurrentLod + 1} ({CarNode.LodsCount} in total; shown from {information?.In.ToInvariantString() ?? "?"} to {information?.Out.ToInvariantString() ?? "?"})",
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset), 0f,
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
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset), 0f,
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, UiColor,
                        CoordinateType.Absolute);
                offset += 20;
            }

            if (CarNode.Skins != null && CarNode.CurrentSkin != null) {
                _textBlock.DrawString($"{CarNode.CurrentSkin} ({CarNode.Skins.IndexOf(CarNode.CurrentSkin) + 1}/{CarNode.Skins.Count})",
                        new RectangleF(0f, 0f, ActualWidth, ActualHeight - offset), 0f,
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, UiColor,
                        CoordinateType.Absolute);
            }
        }

        private Vector3 GetCameraOffsetForCenterAlignment(ICamera camera, bool limited) {
            var nbox = MainSlot.GetCarBoundingBox();
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

        private float _elapsedCamera;
        private float _lastOffset;

        protected override void OnTickOverride(float dt) {
            base.OnTickOverride(dt);

            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                CarSlots[i].CarNode?.OnTick(dt);
            }

            const float threshold = 0.0003f;
            if (_resetState > threshold) {
                /*if (!AutoRotate) {
                    _resetState = 0f;
                    return;
                }*/

                var d = (dt * 10f).Saturate();
                _resetState += (-0f - _resetState) * d;
                if (_resetState <= threshold) {
                    AutoRotate = false;
                }

                var cam = CameraOrbit;
                if (cam != null) {
                    var offset = (_resetCamera.Alpha - cam.Alpha).Abs() + (_resetCamera.Beta - cam.Beta).Abs() + (_resetCamera.Radius - cam.Radius).Abs();
                    if (offset > _lastOffset) {
                        _resetState = 0f;
                        _lastOffset = float.MaxValue;
                    } else {
                        _lastOffset = offset;
                    }

                    cam.Alpha += (_resetCamera.Alpha - cam.Alpha) * d;
                    cam.Beta += (_resetCamera.Beta - cam.Beta) * d;
                    cam.Radius += (_resetCamera.Radius - cam.Radius) * d;
                    cam.Tilt += (_resetCamera.Tilt - cam.Tilt) * d;
                    cam.FovY += (_resetCamera.FovY - cam.FovY) * d;
                    cam.SetLens(cam.Aspect);
                }

                _elapsedCamera = 0f;
                IsDirty = true;
            } else {
                _lastOffset = float.MaxValue;
                if (AutoRotate && CameraOrbit != null) {
                    CameraOrbit.Alpha -= dt * AutoRotateSpeed * 0.29f;
                    CameraOrbit.Beta += ((_elapsedCamera * 0.39f).Sin() * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                    _elapsedCamera += dt * AutoRotateSpeed;

                    IsDirty = true;
                }
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = AutoAdjustedTarget;
                var d = t - CameraOrbit.Target;
                if (d.LengthSquared() > 0.0001 || AutoRotate) {
                    CameraOrbit.Target += (t - CameraOrbit.Target) / (AutoRotate ? 90f : 3f);
                    IsDirty = true;
                }
            }
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _textBlock);
            DisposeHelper.Dispose(ref _shadows);
            DisposeHelper.Dispose(ref _reflectionCubemap);
            _previousCars.SelectMany(x => x.Objects).DisposeEverything();
            _previousCars.Clear();
            base.DisposeOverride();
        }

        public enum CarCameraMode {
            None, FirstPerson, Dashboard, Bonnet, Bumper
        }

        [CanBeNull]
        private CameraBase GetCamera(CarCameraMode mode) {
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
            get => _currentMode;
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
            IsDirty = true;
        }

        private int? _currentExtraCamera;

        public int? CurrentExtraCamera {
            get => _currentExtraCamera;
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
