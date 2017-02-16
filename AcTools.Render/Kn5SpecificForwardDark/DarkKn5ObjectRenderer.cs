using System;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : ToolsKn5ObjectRenderer {
        public Color LightColor { get; set; } = Color.FromArgb(201, 201, 167);

        public Color AmbientDown { get; set; } = Color.FromArgb(82, 136, 191);

        public Color AmbientUp { get; set; } = Color.FromArgb(191, 191, 159);

        private bool _flatMirror = false;

        public bool FlatMirror {
            get { return _flatMirror; }
            set {
                if (value == _flatMirror) return;
                _flatMirror = value;
                OnPropertyChanged();
                RecreateFlatMirror();
            }
        }

        private bool _opaqueGround = true;

        public bool OpaqueGround {
            get { return _opaqueGround; }
            set {
                if (Equals(value, _opaqueGround)) return;
                _opaqueGround = value;
                OnPropertyChanged();
                RecreateFlatMirror();
            }
        }

        public override bool ShowWireframe {
            get { return base.ShowWireframe; }
            set {
                base.ShowWireframe = value;
                (_carWrapper?.ElementAtOrDefault(0) as FlatMirror)?.SetInvertedRasterizerState(
                        value ? DeviceContextHolder.States.WireframeInvertedState : null);
            }
        }

        private float _lightBrightness = 1.5f;

        public float LightBrightness {
            get { return _lightBrightness; }
            set {
                if (Equals(value, _lightBrightness)) return;
                _lightBrightness = value;
                OnPropertyChanged();
            }
        }

        private float _ambientBrightness = 2f;

        public float AmbientBrightness {
            get { return _ambientBrightness; }
            set {
                if (Equals(value, _ambientBrightness)) return;
                _ambientBrightness = value;
                OnPropertyChanged();
            }
        }

        public DarkKn5ObjectRenderer(CarDescription car, string showroomKn5 = null) : base(car, showroomKn5) {
            // UseMsaa = true;
            VisibleUi = false;
            UseSprite = false;
            AllowSkinnedObjects = true;

            //BackgroundColor = Color.FromArgb(10, 15, 25);
            //BackgroundColor = Color.FromArgb(220, 140, 100);

            BackgroundColor = Color.FromArgb(220, 220, 220);
            EnableShadows = EffectDarkMaterial.EnableShadows;
        }

        protected override void OnBackgroundColorChanged() {
            base.OnBackgroundColorChanged();
            UiColor = BackgroundColor.GetBrightness() > 0.5 ? Color.Black : Color.White;
        }

        private static float[] GetSplits(int number, float carSize) {
            switch (number) {
                case 1:
                    return new[] { Math.Max(5f, carSize) };
                case 2:
                    return new[] { 5f, 20f };
                case 3:
                    return new[] { 5f, 20f, 50f };
                case 4:
                    return new[] { 5f, 20f, 50f, 200f };
                default:
                    return new[] { 10f };
            }
        }

        private Kn5RenderableCar _car;
        private RenderableList _carWrapper;

        private void RecreateFlatMirror() {
            if (_carWrapper == null) return;

            var replaceMode = _carWrapper.ElementAtOrDefault(0) is FlatMirror;
            if (replaceMode) {
                _carWrapper.RemoveAt(0);
            }

            var mirrorPlane = new Plane(Vector3.Zero, Vector3.UnitY);
            var mirror = FlatMirror && CarNode != null ? new FlatMirror(CarNode, mirrorPlane) :
                    new FlatMirror(mirrorPlane, OpaqueGround);
            if (FlatMirror && ShowWireframe) {
                mirror.SetInvertedRasterizerState(DeviceContextHolder.States.WireframeInvertedState);
            }

            _carWrapper.Insert(0, mirror);

            if (replaceMode) {
                _carWrapper.UpdateBoundingBox();
            }
        }

        protected override void ExtendCar(Kn5RenderableCar car, RenderableList carWrapper) {
            if (_car != null) {
                _car.ObjectsChanged -= OnCarObjectsChanged;
            }

            base.ExtendCar(car, carWrapper);

            _car = car;
            if (_car != null) {
                _car.ObjectsChanged += OnCarObjectsChanged;
            }

            _carWrapper = carWrapper;
            RecreateFlatMirror();

            if (_meshDebug) {
                UpdateMeshDebug(car);
            }
        }

        private void OnCarObjectsChanged(object sender, EventArgs e) {
            RecreateFlatMirror();
        }

        protected override void PrepareCamera(BaseCamera camera) {
            base.PrepareCamera(camera);

            var orbit = camera as CameraOrbit;
            if (orbit != null) {
                orbit.MinBeta = -0.1f;
                orbit.MinY = 0.05f;
            }

            camera.DisableFrustum = true;
        }

        protected override IMaterialsFactory GetMaterialsFactory() {
            return new MaterialsProviderDark();
        }

        protected override ShadowsDirectional CreateShadows() {
            return new ShadowsDirectional(EffectDarkMaterial.ShadowMapSize,
                    GetSplits(EffectDarkMaterial.NumSplits, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
        }

        protected override ReflectionCubemap CreateReflectionCubemap() {
            return new ReflectionCubemap(1024);
        }

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection) {
            var effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
            effect.FxEyePosW.Set(eyesPosition);
            effect.FxLightDir.Set(light);

            effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);
            effect.FxAmbientDown.Set(AmbientDown.ToVector3() * AmbientBrightness);
            effect.FxAmbientRange.Set((AmbientUp.ToVector3() - AmbientDown.ToVector3()) * AmbientBrightness);
            effect.FxBackgroundColor.Set(BackgroundColor.ToVector3());

            if (shadows != null) {
                effect.FxShadowMaps.SetResourceArray(shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.View).ToArray());
                effect.FxShadowViewProj.SetMatrixArray(
                        shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.ShadowTransform).ToArray());
            }

            if (reflection != null) {
                effect.FxReflectionCubemap.SetResource(reflection.View);
            }
        }

        private bool _suspensionDebug;

        public bool SuspensionDebug {
            get { return _suspensionDebug; }
            set {
                if (Equals(value, _suspensionDebug)) return;
                _suspensionDebug = value;
                OnPropertyChanged();
            }
        }

        private bool _meshDebug;

        public bool MeshDebug {
            get { return _meshDebug; }
            set {
                if (Equals(value, _meshDebug)) return;
                _meshDebug = value;
                OnPropertyChanged();
                UpdateMeshDebug(CarNode);
            }
        }

        private void UpdateMeshDebug([CanBeNull] Kn5RenderableCar carNode) {
            carNode?.SetDebugMode(_meshDebug);
        }

        protected override void DrawScene() {
            base.DrawScene();

            if (SuspensionDebug) {
                CarNode?.DrawSuspensionDebugStuff(DeviceContextHolder, ActualCamera);
            }
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

        protected override Vector3 AutoAdjustedTarget => base.AutoAdjustedTarget + Vector3.UnitY * (SetCameraHigher ? 0f : 0.2f);
    }
}