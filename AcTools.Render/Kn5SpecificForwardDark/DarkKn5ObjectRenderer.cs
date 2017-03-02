using System;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : ToolsKn5ObjectRenderer {
        public Color LightColor { get; set; } = Color.FromArgb(200, 180, 180);

        public Color AmbientDown { get; set; } = Color.FromArgb(150, 180, 180);

        public Color AmbientUp { get; set; } = Color.FromArgb(180, 180, 150);

        /*
        public Color LightColor { get; set; } = Color.FromArgb(201, 201, 167);
        public Color AmbientDown { get; set; } = Color.FromArgb(82, 136, 191);
        public Color AmbientUp { get; set; } = Color.FromArgb(191, 191, 159);*/

        private bool _flatMirror;

        public bool FlatMirror {
            get { return _flatMirror; }
            set {
                if (value == _flatMirror) return;
                _flatMirror = value;
                OnPropertyChanged();
                _mirrorDirty = true;
                UpdateBlurredFlatMirror();
                IsDirty = true;
            }
        }

        private bool _flatMirrorBlurred;

        public bool FlatMirrorBlurred {
            get { return _flatMirrorBlurred; }
            set {
                if (Equals(value, _flatMirrorBlurred)) return;
                _flatMirrorBlurred = value;
                OnPropertyChanged();
                UpdateBlurredFlatMirror();
                IsDirty = true;
            }
        }

        private TargetResourceTexture _mirrorBuffer, _mirrorBlurBuffer, _temporaryBuffer;
        private TargetResourceDepthTexture _mirrorDepthBuffer;

        private void UpdateBlurredFlatMirror() {
            var use = FlatMirror && FlatMirrorBlurred;
            if (use == (_mirrorBuffer != null)) return;

            if (use) {
                _mirrorBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _mirrorBlurBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _temporaryBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _mirrorDepthBuffer = TargetResourceDepthTexture.Create();

                if (!InitiallyResized) return;
                ResizeMirrorBuffers();
            } else {
                DisposeHelper.Dispose(ref _mirrorBuffer);
                DisposeHelper.Dispose(ref _mirrorBlurBuffer);
                DisposeHelper.Dispose(ref _temporaryBuffer);
                DisposeHelper.Dispose(ref _mirrorDepthBuffer);
            }
        }

        private void ResizeMirrorBuffers() {
            _mirrorBuffer?.Resize(DeviceContextHolder, Width, Height, null);
            _mirrorDepthBuffer?.Resize(DeviceContextHolder, Width, Height, null);
            _mirrorBlurBuffer?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            _temporaryBuffer?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
        }

        protected override void ResizeInner() {
            base.ResizeInner();
            ResizeMirrorBuffers();
            ResizeSslrBuffers();
        }

        private bool _opaqueGround = true;

        public bool OpaqueGround {
            get { return _opaqueGround; }
            set {
                if (Equals(value, _opaqueGround)) return;
                _opaqueGround = value;
                OnPropertyChanged();
                _mirrorDirty = true;
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

        private TargetResourceTexture _sslrBufferScene, _sslrBufferResult, _sslrBufferBaseReflection, _sslrBufferNormals;
        private TargetResourceDepthTexture _sslrDepthBuffer;

        private bool _useSslr;

        public bool UseSslr {
            get { return _useSslr; }
            set {
                if (Equals(value, _useSslr)) return;
                _useSslr = value;
                OnPropertyChanged();

                if (value) {
                    _sslrBufferScene = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _sslrBufferResult = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _sslrBufferBaseReflection = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _sslrBufferNormals = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _sslrDepthBuffer = TargetResourceDepthTexture.Create();
                } else {
                    DisposeHelper.Dispose(ref _sslrBufferScene);
                    DisposeHelper.Dispose(ref _sslrBufferResult);
                    DisposeHelper.Dispose(ref _sslrBufferBaseReflection);
                    DisposeHelper.Dispose(ref _sslrBufferNormals);
                    DisposeHelper.Dispose(ref _sslrDepthBuffer);
                }

                if (InitiallyResized) {
                    ResizeSslrBuffers();
                }
            }
        }

        private void ResizeSslrBuffers() {
            _sslrBufferScene?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _sslrBufferResult?.Resize(DeviceContextHolder, Width, Height, null);
            _sslrBufferBaseReflection?.Resize(DeviceContextHolder, Width, Height, null);
            _sslrBufferNormals?.Resize(DeviceContextHolder, Width, Height, null);
            _sslrDepthBuffer?.Resize(DeviceContextHolder, Width, Height, null);
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
                    return new[] { carSize };
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
        private FlatMirror _mirror; 
        private RenderableList _carWrapper;

        private void RecreateFlatMirror() {
            if (_carWrapper == null) return;

            var replaceMode = _carWrapper.ElementAtOrDefault(0) is FlatMirror;
            if (replaceMode) {
                _carWrapper[0].Dispose();
                _carWrapper.RemoveAt(0);
            }

            var mirrorPlane = new Plane(Vector3.Zero, Vector3.UnitY);
            _mirror = FlatMirror && CarNode != null ? new FlatMirror(CarNode, mirrorPlane) :
                    new FlatMirror(mirrorPlane, OpaqueGround);
            if (FlatMirror && ShowWireframe) {
                _mirror.SetInvertedRasterizerState(DeviceContextHolder.States.WireframeInvertedState);
            }

            _carWrapper.Insert(0, _mirror);

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
            _mirrorDirty = true;

            if (_meshDebug) {
                UpdateMeshDebug(car);
            }
        }

        private bool _mirrorDirty;

        private void OnCarObjectsChanged(object sender, EventArgs e) {
            _mirrorDirty = true;
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

        private ShadowsDirectional _shadows;

        protected override ShadowsDirectional CreateShadows() {
            _shadows = new ShadowsDirectional(EffectDarkMaterial.ShadowMapSize,
                    GetSplits(EffectDarkMaterial.NumSplits, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
            return _shadows;
        }

        protected override ReflectionCubemap CreateReflectionCubemap() {
            return new ReflectionCubemap(1024);
        }

        [CanBeNull]
        private EffectDarkMaterial _effect;
        private Vector3 _light;

        private float _reflectionPower = 0.6f;

        public float ReflectionPower {
            get { return _reflectionPower; }
            set {
                if (Equals(value, _reflectionPower)) return;
                _reflectionPower = value;
                OnPropertyChanged();
            }
        }

        protected override void UpdateShadows(ShadowsDirectional shadows, Vector3 center) {
            shadows.SetSplits(DeviceContextHolder, GetSplits(EffectDarkMaterial.NumSplits, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
            base.UpdateShadows(shadows, center);

            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
            }

            _effect.FxShadowMaps.SetResourceArray(shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.View).ToArray());
            _effect.FxShadowViewProj.SetMatrixArray(
                    shadows.Splits.Take(EffectDarkMaterial.NumSplits).Select(x => x.ShadowTransform).ToArray());
        }

        protected override void DrawPrepare() {
            if (_mirrorDirty) {
                _mirrorDirty = false;
                RecreateFlatMirror();
            }

            base.DrawPrepare();
        }

        /*private bool _temporaryFlag;

        public bool TemporaryFlag {
            get { return _temporaryFlag; }
            set {
                if (Equals(value, _temporaryFlag)) return;
                _temporaryFlag = value;
                OnPropertyChanged();
            }
        }*/

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection) {
            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
            }

            _effect.FxEyePosW.Set(eyesPosition);

            _light = light;
            _effect.FxLightDir.Set(light);

            _effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);
            _effect.FxReflectionPower.Set(1f);
            _effect.FxShadowsEnabled.Set(EnableShadows);
            _effect.FxPcssEnabled.Set(EnableShadows && EnablePcssShadows);
            _effect.FxAmbientDown.Set(AmbientDown.ToVector3() * AmbientBrightness);
            _effect.FxAmbientRange.Set((AmbientUp.ToVector3() - AmbientDown.ToVector3()) * AmbientBrightness);
            _effect.FxBackgroundColor.Set(BackgroundColor.ToVector3());

            if (FlatMirror) {
                _effect.FxFlatMirrorPower.Set(ReflectionPower);
            }

            if (reflection != null) {
                _effect.FxReflectionCubemap.SetResource(reflection.View);
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
            if (carNode != null) {
                carNode.DebugMode = _meshDebug;
            }
        }

        private void DrawMirror() {
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            _mirror.DrawReflection(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            _mirror.DrawReflection(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
        }

        protected override void DrawScene() {
            // TODO: support more than one car?

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            var carNode = CarNode;

            // draw reflection if needed
            if (FlatMirror && _mirror != null) {
                if (_effect == null) {
                    _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
                }

                _effect.FxLightDir.Set(new Vector3(_light.X, -_light.Y, _light.Z));

                if (FlatMirrorBlurred) {
                    DeviceContext.ClearDepthStencilView(_mirrorDepthBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                    DeviceContext.ClearRenderTargetView(_mirrorBuffer.TargetView, BackgroundColor);

                    DeviceContext.OutputMerger.SetTargets(_mirrorDepthBuffer.DepthView, _mirrorBuffer.TargetView);

                    DrawMirror();

                    DeviceContext.Rasterizer.SetViewports(OutputViewport);

                    if (UseFxaa) {
                        DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _mirrorBuffer.View, _mirrorBlurBuffer.TargetView);
                        DeviceContextHolder.GetHelper<BlurHelper>()
                                           .BlurFlatMirror(DeviceContextHolder, _mirrorBlurBuffer, _temporaryBuffer, ActualCamera.ViewProjInvert,
                                                   _mirrorDepthBuffer.View, 60f);
                    } else {
                        DeviceContextHolder.GetHelper<BlurHelper>()
                                           .BlurFlatMirror(DeviceContextHolder, _mirrorBuffer, _temporaryBuffer, ActualCamera.ViewProjInvert,
                                                   _mirrorDepthBuffer.View, 60f, target: _mirrorBlurBuffer);
                    }

                    DeviceContextHolder.GetHelper<BlurHelper>()
                                       .BlurFlatMirror(DeviceContextHolder, _mirrorBlurBuffer, _temporaryBuffer, ActualCamera.ViewProjInvert,
                                               _mirrorDepthBuffer.View, 12f);

                    DeviceContext.Rasterizer.SetViewports(Viewport);
                    DeviceContext.OutputMerger.SetTargets(DepthStencilView, InnerBuffer.TargetView);
                } else {
                    DrawMirror();
                }

                _effect.FxLightDir.Set(_light);
            }

            // draw a scene, apart from car
            // TODO

            // draw a mirror
            if (_mirror != null) {
                if (!FlatMirror) {
                    _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.BackgroundGround);
                    _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);
                } else if (FlatMirrorBlurred && _mirrorBuffer != null) {
                    if (_effect == null) {
                        _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
                    }

                    _effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
                    // _effect.FxWorldViewProjInv.SetMatrix(ActualCamera.ViewProjInvert);
                    _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.TextureMirror);
                    _mirror.Draw(DeviceContextHolder, ActualCamera, _mirrorBlurBuffer.View, null, null);
                } else {
                    _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.TransparentMirror);
                    _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
                }
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

            // debug stuff
            if (SuspensionDebug) {
                carNode.DrawSuspensionDebugStuff(DeviceContextHolder, ActualCamera);
            }

            if (carNode.IsColliderVisible) {
                carNode.DrawCollidersDebugStuff(DeviceContextHolder, ActualCamera);
            }
        }

        protected override string GetInformationString() {
            if (SslrAdjustCurrentMode != SslrAdjustMode.None) {
                return $@"Mode: {SslrAdjustCurrentMode}
Start from: {_sslrStartFrom}
Fix multipler: {_sslrFixMultipler}
Offset: {_sslrOffset}
Grow fix: {_sslrGrowFix}
Distance threshold: {_sslrDistanceThreshold}";
            }

            return CarNode?.DebugString ?? base.GetInformationString();
        }

        public enum SslrAdjustMode {
            None, StartFrom, FixMultipler, Offset, GrowFix, DistanceThreshold
        }

        public SslrAdjustMode SslrAdjustCurrentMode;
        private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultipler = 0.7f;
        private float _sslrOffset = 0.048f;
        private float _sslrGrowFix = 0.15f;
        private float _sslrDistanceThreshold = 0.092f;

        public void SslrAdjust(float delta) {
            switch (SslrAdjustCurrentMode) {
                case SslrAdjustMode.None:
                    break;
                case SslrAdjustMode.StartFrom:
                    _sslrStartFrom = (_sslrStartFrom + delta / 10f).Clamp(0.0001f, 0.1f);
                    break;
                case SslrAdjustMode.FixMultipler:
                    _sslrFixMultipler += delta;
                    break;
                case SslrAdjustMode.Offset:
                    _sslrOffset += delta;
                    break;
                case SslrAdjustMode.GrowFix:
                    _sslrGrowFix += delta;
                    break;
                case SslrAdjustMode.DistanceThreshold:
                    _sslrDistanceThreshold += delta;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void DrawSceneToBuffer() {
            if (!UseSslr) {
                base.DrawSceneToBuffer();
                return;
            }

            SetInnerBuffer(_sslrBufferScene);
            base.DrawSceneToBuffer();
            SetInnerBuffer(null);

            DeviceContext.Rasterizer.SetViewports(Viewport);
            DeviceContext.OutputMerger.SetTargets(_sslrDepthBuffer.DepthView, _sslrBufferBaseReflection.TargetView, _sslrBufferNormals.TargetView);
            DeviceContext.ClearDepthStencilView(_sslrDepthBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_sslrBufferNormals.TargetView, (Color4)new Vector4(0.5f));
            DeviceContext.ClearRenderTargetView(_sslrBufferBaseReflection.TargetView, (Color4)new Vector4(0));

            DeviceContext.OutputMerger.DepthStencilState = null;

            var carNode = CarNode;
            if (carNode == null) return;

            _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
            carNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

            var effect = DeviceContextHolder.GetEffect<EffectPpDarkSslr>();
            effect.FxStartFrom.Set(_sslrStartFrom);
            effect.FxFixMultipler.Set(_sslrFixMultipler);
            effect.FxOffset.Set(_sslrOffset);
            effect.FxGlowFix.Set(_sslrGrowFix);
            effect.FxDistanceThreshold.Set(_sslrDistanceThreshold);
            // effect.FxTemporary.Set(TemporaryFlag);

            DeviceContextHolder.GetHelper<DarkSslrHelper>().Draw(DeviceContextHolder,
                    _sslrBufferScene.View,
                    _sslrDepthBuffer.View,
                    _sslrBufferBaseReflection.View,
                    _sslrBufferNormals.View,
                    ActualCamera, _sslrBufferResult.TargetView);
            DeviceContextHolder.GetHelper<BlurHelper>().BlurDarkSslr(DeviceContextHolder, _sslrBufferResult, InnerBuffer, (float)(2f * ResolutionMultipler));
            DeviceContextHolder.GetHelper<DarkSslrHelper>().FinalStep(DeviceContextHolder,
                    _sslrBufferScene.View,
                    _sslrBufferResult.View,
                    _sslrBufferBaseReflection.View,
                    _sslrBufferNormals.View,
                    ActualCamera, InnerBuffer.TargetView);
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

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref _mirrorBuffer);
            DisposeHelper.Dispose(ref _mirrorBlurBuffer);
            DisposeHelper.Dispose(ref _temporaryBuffer);
            DisposeHelper.Dispose(ref _mirrorDepthBuffer);
            DisposeHelper.Dispose(ref _sslrBufferScene);
            DisposeHelper.Dispose(ref _sslrBufferResult);
            DisposeHelper.Dispose(ref _sslrBufferBaseReflection);
            DisposeHelper.Dispose(ref _sslrBufferNormals);
            DisposeHelper.Dispose(ref _sslrDepthBuffer);
        }
    }
}