// #define SSLR_PARAMETRIZED

using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using AcTools.Render.Base;
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
        private Color _lightColor = Color.FromArgb(200, 180, 180);

        public Color LightColor {
            get { return _lightColor; }
            set {
                if (value.Equals(_lightColor)) return;
                _lightColor = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private Color _ambientDown = Color.FromArgb(150, 180, 180);

        public Color AmbientDown {
            get { return _ambientDown; }
            set {
                if (value.Equals(_ambientDown)) return;
                _ambientDown = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private Color _ambientUp = Color.FromArgb(180, 180, 150);

        public Color AmbientUp {
            get { return _ambientUp; }
            set {
                if (value.Equals(_ambientUp)) return;
                _ambientUp = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

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
            ResizeSsaoBuffers();
            ResizeGBuffers();
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
                SetReflectionCubemapDirty();
            }
        }

        private float _ambientBrightness = 2f;

        public float AmbientBrightness {
            get { return _ambientBrightness; }
            set {
                if (Equals(value, _ambientBrightness)) return;
                _ambientBrightness = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private TargetResourceTexture _sslrBufferScene, _sslrBufferResult, _sslrBufferBaseReflection, _gBufferNormals;
        private TargetResourceTexture _gBufferDepth;

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
                    //_sslrDepthBuffer = TargetResourceDepthTexture.Create();
                    if (InitiallyResized) {
                        ResizeSslrBuffers();
                    }
                } else {
                    DisposeHelper.Dispose(ref _sslrBufferScene);
                    DisposeHelper.Dispose(ref _sslrBufferResult);
                    DisposeHelper.Dispose(ref _sslrBufferBaseReflection);
                    //DisposeHelper.Dispose(ref _sslrDepthBuffer);
                }

                UpdateGBuffers();
            }
        }

        private void ResizeSslrBuffers() {
            _sslrBufferScene?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _sslrBufferResult?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _sslrBufferBaseReflection?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
        }

        private TargetResourceTexture _ssaoBuffer;

        private bool _useSsao;

        public bool UseSsao {
            get { return _useSsao; }
            set {
                if (Equals(value, _useSsao)) return;
                _useSsao = value;
                OnPropertyChanged();

                if (value) {
                    _ssaoBuffer = TargetResourceTexture.Create(Format.R8_UNorm);
                    if (InitiallyResized) {
                        ResizeSsaoBuffers();
                    }
                } else {
                    _effect?.FxEnableAo.Set(false);
                    DisposeHelper.Dispose(ref _ssaoBuffer);
                }

                UpdateGBuffers();
            }
        }

        private bool _ssaoDebug;

        public bool SsaoDebug {
            get { return _ssaoDebug; }
            set {
                if (Equals(value, _ssaoDebug)) return;
                _ssaoDebug = value;
                OnPropertyChanged();
            }
        }

        private void ResizeSsaoBuffers() {
            _ssaoBuffer?.Resize(DeviceContextHolder, Width, Height, null);
        }

        private void UpdateGBuffers() {
            var value = UseSslr || UseSsao;
            if (_gBufferNormals != null == value) return;
            
            if (value) {
                _gBufferNormals = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _gBufferDepth = TargetResourceTexture.Create(Format.R32_Float);
            } else {
                DisposeHelper.Dispose(ref _gBufferNormals);
                DisposeHelper.Dispose(ref _gBufferDepth);
            }

            if (InitiallyResized) {
                ResizeGBuffers();
            }
        }

        private void ResizeGBuffers() {
            _gBufferNormals?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
            _gBufferDepth?.Resize(DeviceContextHolder, Width, Height, SampleDescription);
        }

        private readonly bool _showroom;

        public DarkKn5ObjectRenderer(CarDescription car, string showroomKn5 = null) : base(car, showroomKn5) {
            // UseMsaa = true;
            VisibleUi = false;
            UseSprite = false;
            AllowSkinnedObjects = true;

            if (showroomKn5 != null) {
                _showroom = true;
                CubemapReflection = true;
            }

            //BackgroundColor = Color.FromArgb(10, 15, 25);
            //BackgroundColor = Color.FromArgb(220, 140, 100);

            BackgroundColor = Color.FromArgb(220, 220, 220);
            BackgroundBrightness = showroomKn5 == null ? 1f : 2f;
            EnableShadows = EffectDarkMaterial.EnableShadows;

#if DEBUG
            //FlatMirror = true;
            //FlatMirrorBlurred = true;
            //ReflectionPower = 1.0f;
            //EnablePcssShadows = true;
            //UseSsao = true;
#endif
        }

        protected override void OnBackgroundColorChanged() {
            base.OnBackgroundColorChanged();
            SetReflectionCubemapDirty();
            UiColor = BackgroundColor.GetBrightness() > 0.5 && !_showroom ? Color.Black : Color.White;
        }

        private static float[] GetSplits(int number, float carSize) {
            if (carSize > 10f) carSize = 10f;
            switch (number) {
                case 1:
                    return new[] { carSize };
                case 2:
                    return new[] { carSize, 20f };
                case 3:
                    return new[] { carSize, 20f, 50f };
                case 4:
                    return new[] { carSize, 20f, 50f, 200f };
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
            var splits = GetShadowsNumSplits();
            _shadows = new ShadowsDirectional(ShadowMapSize,
                    GetSplits(splits, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
            return _shadows;
        }

        protected override ReflectionCubemap CreateReflectionCubemap() {
            return new ReflectionCubemap(2048);
        }

        [CanBeNull]
        private EffectDarkMaterial _effect;
        private Vector3 _light;

        private float _flatMirrorReflectiveness = 0.6f;

        public float FlatMirrorReflectiveness {
            get { return _flatMirrorReflectiveness; }
            set {
                if (Equals(value, _flatMirrorReflectiveness)) return;
                _flatMirrorReflectiveness = value;
                OnPropertyChanged();
            }
        }

        private int _shadowMapSize = 2048;

        public int ShadowMapSize {
            get { return _shadowMapSize; }
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                OnPropertyChanged();
                SetShadowsDirty();
            }
        }

        private int GetShadowsNumSplits() {
            return ShowroomNode == null ? 1 : 3;
        }

        protected override void UpdateShadows(ShadowsDirectional shadows, Vector3 center) {
            var splits = GetShadowsNumSplits();
            shadows.SetSplits(DeviceContextHolder, GetSplits(splits, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
            shadows.SetMapSize(DeviceContextHolder, ShadowMapSize);

            base.UpdateShadows(shadows, center);

            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
            }

            _effect.FxShadowMapSize.Set(ShadowMapSize);
            _effect.FxShadowMaps.SetResourceArray(shadows.Splits.Take(splits).Select(x => x.View).ToArray());
            _effect.FxShadowViewProj.SetMatrixArray(
                    shadows.Splits.Take(splits).Select(x => x.ShadowTransform).ToArray());
        }

        protected override void DrawPrepare() {
            if (_mirrorDirty) {
                _mirrorDirty = false;
                RecreateFlatMirror();
            }

            base.DrawPrepare();
        }

        public override void DrawSceneForShadows(DeviceContextHolder holder, ICamera camera) {
            ShowroomNode?.Draw(holder, camera, SpecialRenderMode.Shadow);
            CarNode?.Draw(holder, camera, SpecialRenderMode.Shadow);
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

        private float _materialsReflectiveness = 1f;

        public float MaterialsReflectiveness {
            get { return _materialsReflectiveness; }
            set {
                if (Equals(value, _materialsReflectiveness)) return;
                _materialsReflectiveness = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private bool _reflectionsWithShadows;

        public bool ReflectionsWithShadows {
            get { return _reflectionsWithShadows; }
            set {
                if (Equals(value, _reflectionsWithShadows)) return;
                _reflectionsWithShadows = value;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private bool _cubemapAmbient;

        public bool CubemapAmbient {
            get { return _cubemapAmbient; }
            set {
                if (Equals(value, _cubemapAmbient)) return;
                _cubemapAmbient = value;
                OnPropertyChanged();
            }
        }

        public override void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            if (ShowroomNode == null) return;
            DrawPrepareEffect(camera.Position, Light, ReflectionsWithShadows ? _shadows : null, null);
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.InvertedState;
            ShowroomNode.Draw(holder, camera, SpecialRenderMode.Simple);
            DeviceContext.Rasterizer.State = null;
        }

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection) {
            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
            }

            _effect.FxEyePosW.Set(eyesPosition);

            _light = light;
            _effect.FxLightDir.Set(light);

            _effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);
            _effect.FxReflectionPower.Set(MaterialsReflectiveness);
            _effect.FxCubemapReflections.Set(reflection != null);
            _effect.FxCubemapAmbient.Set(CubemapAmbient && reflection != null);
            _effect.FxNumSplits.Set(EnableShadows && shadows != null ? GetShadowsNumSplits() : 0);
            _effect.FxPcssEnabled.Set(EnableShadows && UsePcss);
            _effect.FxAmbientDown.Set(AmbientDown.ToVector3() * AmbientBrightness);
            _effect.FxAmbientRange.Set((AmbientUp.ToVector3() - AmbientDown.ToVector3()) * AmbientBrightness);
            _effect.FxBackgroundColor.Set(BackgroundColor.ToVector3() * BackgroundBrightness);

            if (FlatMirror) {
                _effect.FxFlatMirrorPower.Set(FlatMirrorReflectiveness);
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
            if (ShowroomNode == null && FlatMirror && _mirror != null) {
                if (_effect == null) {
                    _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
                }

                _effect.FxLightDir.Set(new Vector3(_light.X, -_light.Y, _light.Z));

                if (FlatMirrorBlurred) {
                    DeviceContext.ClearDepthStencilView(_mirrorDepthBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                    DeviceContext.ClearRenderTargetView(_mirrorBuffer.TargetView, (Color4)BackgroundColor * BackgroundBrightness);

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
            if (ShowroomNode != null) {
                if (_effect == null) {
                    _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
                }

                if (CubemapAmbient) {
                    _effect.FxCubemapAmbient.Set(false);
                }

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

                if (CubemapAmbient) {
                    _effect.FxCubemapAmbient.Set(CubemapAmbient);
                }
            } else {
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
#if SSLR_PARAMETRIZED
            if (SslrAdjustCurrentMode != SslrAdjustMode.None) {
                return $@"Mode: {SslrAdjustCurrentMode}
Start from: {_sslrStartFrom}
Fix multiplier: {_sslrFixMultiplier}
Offset: {_sslrOffset}
Grow fix: {_sslrGrowFix}
Distance threshold: {_sslrDistanceThreshold}";
            }
#endif

            var aa = new[] {
                UseMsaa ? MsaaSampleCount + "xMSAA" : null,
                UseSsaa ? $"{Math.Pow(ResolutionMultiplier, 2d).Round()}xSSAA{(TemporaryFlag ? " (exp.)" : "")}" : null,
                UseFxaa ? "FXAA" : null,
            }.NonNull().JoinToString(", ");

            var se = new[] {
                UseSslr ? "SSLR" : null,
                UseSsao ? "SSAO" : null,
                UseBloom ? "Bloom" : null,
            }.NonNull().JoinToString(", ");

            var pp = new[] {
                UseToneMapping ? "Tone Mapping" : null,
                UseColorGrading && ColorGradingFilename != null ? "Color Grading" : null
            }.NonNull().JoinToString(", ");

            if (UseToneMapping) {
                pp += $"\r\nExp./Gamma/White P.: {ToneExposure:F2}, {ToneGamma:F2}, {ToneWhitePoint:F2}";
            }

            return CarNode?.DebugString ?? $@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
Triangles: {CarNode?.TrianglesCount:D}
AA: {(string.IsNullOrEmpty(aa) ? "None" : aa)}
Shadows: {(EnableShadows ? $"{(UsePcss ? "Yes, PCSS" : "Yes")} ({ShadowMapSize})" : "No")}
Effects: {(string.IsNullOrEmpty(se) ? "None" : se)}
Color: {(string.IsNullOrWhiteSpace(pp) ? "Original" : pp)}".Trim();
        }

#if SSLR_PARAMETRIZED
        public enum SslrAdjustMode {
            None, StartFrom, FixMultiplier, Offset, GrowFix, DistanceThreshold
        }

        public SslrAdjustMode SslrAdjustCurrentMode;
        private bool _sslrParamsChanged = true;
        private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultiplier = 0.7f;
        private float _sslrOffset = 0.048f;
        private float _sslrGrowFix = 0.15f;
        private float _sslrDistanceThreshold = 0.092f;

        public void SslrAdjust(float delta) {
            switch (SslrAdjustCurrentMode) {
                case SslrAdjustMode.None:
                    return;
                case SslrAdjustMode.StartFrom:
                    _sslrStartFrom = (_sslrStartFrom + delta / 10f).Clamp(0.0001f, 0.1f);
                    break;
                case SslrAdjustMode.FixMultiplier:
                    _sslrFixMultiplier += delta;
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

            _sslrParamsChanged = true;
        }
#endif

        private DarkSslrHelper _sslrHelper;
        private DarkSsaoHelper _ssaoHelper;
        private BlurHelper _blurHelper;

        protected void DrawPreparedSceneToBuffer() {
            DeviceContext.ClearRenderTargetView(InnerBuffer.TargetView, (Color4)BackgroundColor * BackgroundBrightness);

            if (DepthStencilView != null) {
                DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
                DeviceContext.OutputMerger.SetTargets(DepthStencilView, InnerBuffer.TargetView);
            } else {
                DeviceContext.OutputMerger.SetTargets(InnerBuffer.TargetView);
            }

            DrawScene();
            DrawAfter();

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;
        }

        protected override void DrawSceneToBuffer() {
            if (!UseSslr && !UseSsao) {
                base.DrawSceneToBuffer();
                return;
            }

            DrawPrepare();

            if (_sslrHelper == null) {
                _sslrHelper = DeviceContextHolder.GetHelper<DarkSslrHelper>();
            }

            if (_ssaoHelper == null) {
                _ssaoHelper = DeviceContextHolder.GetHelper<DarkSsaoHelper>();
            }

            if (_blurHelper == null) {
                _blurHelper = DeviceContextHolder.GetHelper<BlurHelper>();
            }

            // Draw scene to G-buffer to get normals, depth and base reflection
            DeviceContext.Rasterizer.SetViewports(Viewport);
            DeviceContext.OutputMerger.SetTargets(DepthStencilView, _sslrBufferBaseReflection?.TargetView, _gBufferNormals.TargetView, _gBufferDepth.TargetView);
            DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            DeviceContext.ClearRenderTargetView(_gBufferNormals.TargetView, (Color4)new Vector4(0.5f));

            if (_sslrBufferBaseReflection != null) {
                DeviceContext.ClearRenderTargetView(_sslrBufferBaseReflection.TargetView, (Color4)new Vector4(0));
            }

            DeviceContext.ClearRenderTargetView(_gBufferDepth.TargetView, (Color4)new Vector4(1f));

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;

            if (ShowroomNode != null) {
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
            } else {
                if (_mirror != null) {
                    if (FlatMirror && !FlatMirrorBlurred) {
                        _mirror.DrawReflection(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
                    } else {
                        _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
                    }
                }
            }

            CarNode?.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

            // AO?
            if (UseSsao) {
                _ssaoHelper.Draw(DeviceContextHolder,
                        _gBufferDepth.View,
                        _gBufferNormals.View,
                        ActualCamera, _ssaoBuffer.TargetView);

                if (_effect == null) {
                    _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>();
                }

                _effect.FxAoMap.SetResource(_ssaoBuffer.View);
                _effect.FxEnableAo.Set(true);
                _effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

                _ssaoHelper.Blur(DeviceContextHolder, _ssaoBuffer, InnerBuffer, Camera);

                if (SsaoDebug) {
                    DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _ssaoBuffer.View, InnerBuffer.TargetView);
                    return;
                }
            }

            if (UseSslr && _sslrBufferBaseReflection != null) {
                // Draw actual scene to _sslrBufferScene
                SetInnerBuffer(_sslrBufferScene);
                DrawPreparedSceneToBuffer();
                SetInnerBuffer(null);

                // Prepare SSLR and combine buffers
#if SSLR_PARAMETRIZED
                if (_sslrParamsChanged) {
                    _sslrParamsChanged = false;
                    var effect = DeviceContextHolder.GetEffect<EffectPpDarkSslr>();
                    effect.FxStartFrom.Set(_sslrStartFrom);
                    effect.FxFixMultiplier.Set(_sslrFixMultiplier);
                    effect.FxOffset.Set(_sslrOffset);
                    effect.FxGlowFix.Set(_sslrGrowFix);
                    effect.FxDistanceThreshold.Set(_sslrDistanceThreshold);
                }
#endif

                _sslrHelper.Draw(DeviceContextHolder,
                        _gBufferDepth.View,
                        _sslrBufferBaseReflection.View,
                        _gBufferNormals.View,
                        ActualCamera, _sslrBufferResult.TargetView);
                _blurHelper.BlurDarkSslr(DeviceContextHolder, _sslrBufferResult, InnerBuffer, (float)(2f * ResolutionMultiplier));
                _sslrHelper.FinalStep(DeviceContextHolder,
                        _sslrBufferScene.View,
                        _sslrBufferResult.View,
                        _sslrBufferBaseReflection.View,
                        _gBufferNormals.View,
                        ActualCamera, InnerBuffer.TargetView);
            } else {
                DrawPreparedSceneToBuffer();
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

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref _mirrorBuffer);
            DisposeHelper.Dispose(ref _mirrorBlurBuffer);
            DisposeHelper.Dispose(ref _temporaryBuffer);
            DisposeHelper.Dispose(ref _mirrorDepthBuffer);

            DisposeHelper.Dispose(ref _sslrBufferScene);
            DisposeHelper.Dispose(ref _sslrBufferResult);
            DisposeHelper.Dispose(ref _sslrBufferBaseReflection);
            DisposeHelper.Dispose(ref _gBufferNormals);
            DisposeHelper.Dispose(ref _gBufferDepth);
            DisposeHelper.Dispose(ref _ssaoBuffer);
            //DisposeHelper.Dispose(ref _sslrDepthBuffer);
        }
    }
}