// #define SSLR_PARAMETRIZED

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.PostEffects.AO;
using AcTools.Render.Base.Reflections;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
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
    public enum AoType {
        [Description("SSAO")]
        Ssao,

        [Description("SSAO (Alt.)")]
        SsaoAlt,

        [Description("HBAO")]
        Hbao,

        [Description("ASSAO")]
        Assao
    }

    public partial class DarkKn5ObjectRenderer : ToolsKn5ObjectRenderer {
        public static readonly AoType[] ProductionReadyAo = {
            AoType.Ssao, AoType.SsaoAlt
        };

        private Color _lightColor = Color.FromArgb(200, 180, 180);

        public Color LightColor {
            get { return _lightColor; }
            set {
                if (value.Equals(_lightColor)) return;
                _lightColor = value;
                IsDirty = true;
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
                IsDirty = true;
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
                IsDirty = true;
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
                IsDirty = true;
                OnPropertyChanged();
                _mirrorDirty = true;
                UpdateBlurredFlatMirror();
            }
        }

        private bool _flatMirrorBlurred;

        public bool FlatMirrorBlurred {
            get { return _flatMirrorBlurred; }
            set {
                if (Equals(value, _flatMirrorBlurred)) return;
                _flatMirrorBlurred = value;
                IsDirty = true;
                OnPropertyChanged();
                UpdateBlurredFlatMirror();
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
                IsDirty = true;
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
                IsDirty = true;
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
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        public bool GBufferMsaa { get; set; } = true;

        private TargetResourceTexture _sslrBufferScene, _sslrBufferResult, _sslrBufferBaseReflection;

        private bool _useSslr;

        public bool UseSslr {
            get { return _useSslr; }
            set {
                if (Equals(value, _useSslr)) return;
                _useSslr = value;
                IsDirty = true;
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
            var sample = GBufferMsaa ? SampleDescription : (SampleDescription?)null;
            _sslrBufferScene?.Resize(DeviceContextHolder, Width, Height, sample);
            _sslrBufferResult?.Resize(DeviceContextHolder, Width, Height, sample);
            _sslrBufferBaseReflection?.Resize(DeviceContextHolder, Width, Height, sample);
        }

        private TargetResourceTexture _aoBuffer;

        [CanBeNull]
        private AoHelperBase _aoHelper;

        private float _aoOpacity = 0.3f;

        public float AoOpacity {
            get { return _aoOpacity; }
            set {
                if (Equals(value, _aoOpacity)) return;
                _aoOpacity = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private bool _useAo;

        public bool UseAo {
            get { return _useAo; }
            set {
                if (Equals(value, _useAo)) return;
                _useAo = value;
                IsDirty = true;
                OnPropertyChanged();

                if (!value) {
                    Effect.FxAoPower.Set(0f);
                }

                RecreateAoBuffer();
                UpdateGBuffers();
            }
        }

        private bool _aoDebug;

        public bool AoDebug {
            get { return _aoDebug; }
            set {
                if (Equals(value, _aoDebug)) return;
                _aoDebug = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private AoType _aoType = AoType.Ssao;

        public AoType AoType {
            get { return _aoType; }
            set {
#if !DEBUG
                if (!ProductionReadyAo.Contains(value)) value = AoType.Ssao;
#endif

                if (Equals(value, _aoType)) return;
                _aoType = value;
                IsDirty = true;
                OnPropertyChanged();
                RecreateAoBuffer();
            }
        }

        private void RecreateAoBuffer() {
            if (!UseAo) {
                DisposeHelper.Dispose(ref _aoBuffer);
                return;
            }

            Format format;
            switch (AoType) {
                case AoType.Ssao:
                case AoType.SsaoAlt:
                    format = Format.R8_UNorm;
                    break;
                default:
                    format = Format.R8G8B8A8_UNorm;
                    break;
            }

            _aoHelper = null;
            if (_aoBuffer == null || _aoBuffer.Format != format) {
                DisposeHelper.Dispose(ref _aoBuffer);
                _aoBuffer = TargetResourceTexture.Create(format);
            }

            if (InitiallyResized) {
                ResizeSsaoBuffers();
            }
        }

        [NotNull]
        private AoHelperBase GetAoHelper() {
            switch (AoType) {
                case AoType.Ssao:
                    return DeviceContextHolder.GetHelper<SsaoHelper>();
                case AoType.SsaoAlt:
                    return DeviceContextHolder.GetHelper<SsaoAltHelper>();
                case AoType.Hbao:
                    return DeviceContextHolder.GetHelper<HbaoHelper>();
                case AoType.Assao:
                    return DeviceContextHolder.GetHelper<AssaoHelper>();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ResizeSsaoBuffers() {
            _aoBuffer?.Resize(DeviceContextHolder, Width, Height, null);
        }

        private TargetResourceTexture _gBufferNormals, _gBufferDepthAlt;
        private TargetResourceDepthTexture _gBufferDepthD;

        private void UpdateGBuffers() {
            var value = UseSslr || UseAo;
            if (_gBufferNormals != null == value) return;

            if (value) {
                _gBufferNormals = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _gBufferDepthAlt = TargetResourceTexture.Create(Format.R32_Float);
                _gBufferDepthD = TargetResourceDepthTexture.Create();
            } else {
                DisposeHelper.Dispose(ref _gBufferNormals);
                DisposeHelper.Dispose(ref _gBufferDepthAlt);
                DisposeHelper.Dispose(ref _gBufferDepthD);
            }

            if (InitiallyResized) {
                ResizeGBuffers();
            }
        }

        private void ResizeGBuffers() {
            var sample = GBufferMsaa ? SampleDescription : (SampleDescription?)null;
            _gBufferNormals?.Resize(DeviceContextHolder, Width, Height, sample);
            // _gBufferDepthAlt?.Resize(DeviceContextHolder, Width, Height, sample);
            // _gBufferDepthD?.Resize(DeviceContextHolder, Width, Height, sample);
        }

        private readonly bool _showroom;

        public DarkKn5ObjectRenderer(CarDescription car, string showroomKn5 = null) : base(car, showroomKn5) {
            // UseMsaa = true;
            AllowSkinnedObjects = true;

            if (showroomKn5 != null) {
                _showroom = true;
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
                    GetSplits(splits ?? 1, splits.HasValue ? CarNode?.BoundingBox?.GetSize().Length() ?? 4f : 1000f));
            return _shadows;
        }

        protected override ReflectionCubemap CreateReflectionCubemap() {
            return new ReflectionCubemap(2048);
        }

        [NotNull]
        private EffectDarkMaterial Effect => _effect ?? (_effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>());
        private EffectDarkMaterial _effect;

        private Vector3 _light;

        private float _flatMirrorReflectiveness = 0.6f;

        public float FlatMirrorReflectiveness {
            get { return _flatMirrorReflectiveness; }
            set {
                if (Equals(value, _flatMirrorReflectiveness)) return;
                _flatMirrorReflectiveness = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private int _shadowMapSize = 2048;

        public int ShadowMapSize {
            get { return _shadowMapSize; }
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                IsDirty = true;
                OnPropertyChanged();
                SetShadowsDirty();
            }
        }

        private int? GetShadowsNumSplits() {
            // just a car — single cascade
            if (ShowroomNode == null) return 1;

            // showroom doesn’t cast shadows — single cascade
            if (ShowroomNode.Meshes.All(x => !x.OriginalNode.CastShadows)) return 1;

            // showroom casts shadows all over the scene
            if (ShowroomNode.OriginalFile.Materials.Values.All(x =>
                    x.GetPropertyByName("ksDiffuse")?.ValueA == 0f && x.GetPropertyByName("ksAmbient")?.ValueA >= 1f)) return null;

            return 3;
        }

        private int _numSplits;

        protected override void UpdateShadows(ShadowsDirectional shadows, Vector3 center) {
            _pcssParamsSet = false;

            var splits = GetShadowsNumSplits();
            if (splits == null) {
                // everything is shadowed
                _numSplits = -1;
            } else {
                shadows.SetSplits(DeviceContextHolder, GetSplits(splits.Value, CarNode?.BoundingBox?.GetSize().Length() ?? 4f));
                shadows.SetMapSize(DeviceContextHolder, ShadowMapSize);
                base.UpdateShadows(shadows, center);

                _numSplits = splits.Value;

                var effect = Effect;
                effect.FxShadowMapSize.Set(new Vector2(ShadowMapSize, 1f / ShadowMapSize));
                effect.FxShadowMaps.SetResourceArray(shadows.Splits.Take(splits.Value).Select(x => x.View).ToArray());
                effect.FxShadowViewProj.SetMatrixArray(
                        shadows.Splits.Take(splits.Value).Select(x => x.ShadowTransform).ToArray());
            }
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
                IsDirty = true;
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
                IsDirty = true;
                OnPropertyChanged();
                SetReflectionCubemapDirty();
            }
        }

        private bool _cubemapAmbientWhite = true;

        public bool CubemapAmbientWhite {
            get { return _cubemapAmbientWhite; }
            set {
                if (Equals(value, _cubemapAmbientWhite)) return;
                _cubemapAmbientWhite = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private float _cubemapAmbient = 0.5f;

        public float CubemapAmbient {
            get { return _cubemapAmbient; }
            set {
                if (Equals(value, _cubemapAmbient)) return;
                _cubemapAmbient = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        public override void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            var showroomNode = ShowroomNode;
            if (showroomNode == null) return;

            if (UseAo) {
                Effect.FxAoPower.Set(0f);
            }

            DrawPrepareEffect(camera.Position, Light, ReflectionsWithShadows ? _shadows : null, null);
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.InvertedState;
            showroomNode.Draw(holder, camera, SpecialRenderMode.Reflection);
            DeviceContext.Rasterizer.State = null;
        }

        private float _pcssLightScale = 2f;

        public float PcssLightScale {
            get { return _pcssLightScale; }
            set {
                if (Equals(value, _pcssLightScale)) return;
                _pcssLightScale = value;
                IsDirty = true;
                _pcssParamsSet = false;
                OnPropertyChanged();
            }
        }

        private float _pcssSceneScale = 0.06f;

        public float PcssSceneScale {
            get { return _pcssSceneScale; }
            set {
                if (Equals(value, _pcssSceneScale)) return;
                _pcssSceneScale = value;
                IsDirty = true;
                _pcssParamsSet = false;
                OnPropertyChanged();
            }
        }

        private float FxCubemapAmbientValue => CubemapAmbientWhite ? -CubemapAmbient : CubemapAmbient;

        private bool _pcssNoiseMapSet, _pcssParamsSet;

        private void PreparePcss(ShadowsDirectional shadows) {
            if (_pcssParamsSet) return;
            _pcssParamsSet = true;

            var splits = new Vector4[shadows.Splits.Length];
            var sceneScale = (ShowroomNode == null ? 1f : 2f) * PcssSceneScale;
            var lightScale = PcssLightScale;
            for (var i = 0; i < shadows.Splits.Length; i++) {
                splits[i] = new Vector4(sceneScale / shadows.Splits[i].Size, lightScale / shadows.Splits[i].Size, 0, 0);
            }

            var effect = Effect;
            effect.FxPcssScale.Set(splits);

            if (!_pcssNoiseMapSet) {
                _pcssNoiseMapSet = true;
                effect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(16, 16));
            }
        }

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection) {
            var effect = Effect;
            effect.FxEyePosW.Set(eyesPosition);

            _light = light;
            effect.FxLightDir.Set(light);

            effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);
            effect.FxReflectionPower.Set(MaterialsReflectiveness);
            effect.FxCubemapReflections.Set(reflection != null);
            effect.FxCubemapAmbient.Set(reflection == null ? 0f : FxCubemapAmbientValue);

            // shadows
            var useShadows = EnableShadows && shadows != null;
            effect.FxNumSplits.Set(useShadows ? _numSplits : 0);

            if (useShadows) {
                effect.FxPcssEnabled.Set(UsePcss);
                if (UsePcss) {
                    PreparePcss(shadows);
                }
            }

            // colors
            effect.FxAmbientDown.Set(AmbientDown.ToVector3() * AmbientBrightness);
            effect.FxAmbientRange.Set((AmbientUp.ToVector3() - AmbientDown.ToVector3()) * AmbientBrightness);
            effect.FxBackgroundColor.Set(BackgroundColor.ToVector3() * BackgroundBrightness);

            // flat mirror
            if (FlatMirror && ShowroomNode == null) {
                effect.FxFlatMirrorPower.Set(FlatMirrorReflectiveness);
            }

            if (reflection != null) {
                effect.FxReflectionCubemap.SetResource(reflection.View);
            }
        }

        private bool _meshDebug;

        public bool MeshDebug {
            get { return _meshDebug; }
            set {
                if (Equals(value, _meshDebug)) return;
                _meshDebug = value;
                IsDirty = true;
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
            var effect = Effect;

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            var carNode = CarNode;

            // draw reflection if needed
            if (ShowroomNode == null && FlatMirror && _mirror != null) {
                effect.FxLightDir.Set(new Vector3(_light.X, -_light.Y, _light.Z));

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

                effect.FxLightDir.Set(_light);
            }

            // draw a scene, apart from car
            if (ShowroomNode != null) {
                if (CubemapAmbient != 0f) {
                    effect.FxCubemapAmbient.Set(0f);
                }

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
                ShowroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

                if (CubemapAmbient != 0f) {
                    effect.FxCubemapAmbient.Set(FxCubemapAmbientValue);
                }
            } else {
                // draw a mirror
                if (_mirror != null) {
                    if (!FlatMirror) {
                        _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.BackgroundGround);
                        _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);
                    } else if (FlatMirrorBlurred && _mirrorBuffer != null) {
                        effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
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
            carNode.DrawDebug(DeviceContextHolder, ActualCamera);
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
                UseSsaa ? $"{Math.Pow(ResolutionMultiplier, 2d).Round()}xSSAA" : null,
                UseFxaa ? "FXAA" : null,
            }.NonNull().JoinToString(", ");

            var se = new[] {
                UseSslr ? "SSLR" : null,
                UseAo ? AoType.GetDescription() : null,
                UseBloom ? "Bloom" : null,
            }.NonNull().JoinToString(", ");

            var pp = new[] {
                ToneMapping != ToneMappingFn.None ? "Tone Mapping" : null,
                UseColorGrading && ColorGradingData != null ? "Color Grading" : null
            }.NonNull().JoinToString(", ");

            if (ToneMapping != ToneMappingFn.None) {
                pp += $"\r\nTone Mapping Func.: {ToneMapping.GetDescription()}";
                pp += $"\r\nExp./Gamma/White P.: {ToneExposure:F2}, {ToneGamma:F2}, {ToneWhitePoint:F2}";
            }

            return CarNode?.DebugString ?? $@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")} ({Width}×{Height})
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

        /*private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultiplier = 0.7f;
        private float _sslrOffset = 0.048f;
        private float _sslrGrowFix = 0.15f;
        private float _sslrDistanceThreshold = 0.092f;*/

        private float _sslrStartFrom = 0.02f;
        private float _sslrFixMultiplier = 0.5f;
        private float _sslrOffset = 0.05f;
        private float _sslrGrowFix = 0.1f;
        private float _sslrDistanceThreshold = 0.01f;

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
            if (!UseSslr && !UseAo) {
                base.DrawSceneToBuffer();
                return;
            }

            DrawPrepare();

            if (_sslrHelper == null) {
                _sslrHelper = DeviceContextHolder.GetHelper<DarkSslrHelper>();
            }

            if (_blurHelper == null) {
                _blurHelper = DeviceContextHolder.GetHelper<BlurHelper>();
            }

            // Draw scene to G-buffer to get normals, depth and base reflection
            DeviceContext.Rasterizer.SetViewports(Viewport);

            var sample = GBufferMsaa ? SampleDescription : (SampleDescription?)null;
            ShaderResourceView depth;
            if (UseMsaa && GBufferMsaa) {
                _gBufferDepthAlt.Resize(DeviceContextHolder, Width, Height, sample);
                DeviceContext.OutputMerger.SetTargets(DepthStencilView, _sslrBufferBaseReflection?.TargetView, _gBufferNormals.TargetView,
                        _gBufferDepthAlt.TargetView);
                DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                DeviceContext.ClearRenderTargetView(_gBufferDepthAlt.TargetView, (Color4)new Vector4(1f));
                depth = _gBufferDepthAlt.View;
            } else {
                _gBufferDepthD.Resize(DeviceContextHolder, Width, Height, sample);
                DeviceContext.OutputMerger.SetTargets(_gBufferDepthD.DepthView, _sslrBufferBaseReflection?.TargetView, _gBufferNormals.TargetView);
                DeviceContext.ClearDepthStencilView(_gBufferDepthD.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                depth = _gBufferDepthD.View;
            }

            DeviceContext.ClearRenderTargetView(_gBufferNormals.TargetView, (Color4)new Vector4(0.5f));

            if (_sslrBufferBaseReflection != null) {
                DeviceContext.ClearRenderTargetView(_sslrBufferBaseReflection.TargetView, (Color4)new Vector4(0));
            }

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
            if (UseAo) {
                var aoHelper = _aoHelper;
                if (aoHelper == null) {
                    aoHelper = _aoHelper = GetAoHelper();
                }

                if (AoType == AoType.Hbao) {
                    UseSslr = true;
                    SetInnerBuffer(_sslrBufferScene);
                    DrawPreparedSceneToBuffer();
                    (aoHelper as HbaoHelper)?.Prepare(DeviceContextHolder, _sslrBufferScene.View);
                    SetInnerBuffer(null);
                }

                aoHelper.Draw(DeviceContextHolder, depth, _gBufferNormals.View, ActualCamera, _aoBuffer.TargetView);
                aoHelper.Blur(DeviceContextHolder, _aoBuffer, InnerBuffer, Camera);

                var effect = Effect;
                effect.FxAoMap.SetResource(_aoBuffer.View);
                Effect.FxAoPower.Set(AoOpacity);
                effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

                if (AoDebug) {
                    DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _aoBuffer.View, InnerBuffer.TargetView);
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
                
                _sslrHelper.Draw(DeviceContextHolder, depth, _sslrBufferBaseReflection.View, _gBufferNormals.View, ActualCamera,
                        _sslrBufferResult.TargetView);
                _blurHelper.BlurDarkSslr(DeviceContextHolder, _sslrBufferResult, InnerBuffer, (float)(4f * ResolutionMultiplier));
                _sslrHelper.FinalStep(DeviceContextHolder, _sslrBufferScene.View, _sslrBufferResult.View, _sslrBufferBaseReflection.View,
                        _gBufferNormals.View, ActualCamera, InnerBuffer.TargetView);
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
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        protected override Vector3 AutoAdjustedTarget => base.AutoAdjustedTarget + Vector3.UnitY * (SetCameraHigher ? 0f : 0.2f);

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _mirror);
            DisposeHelper.Dispose(ref _mirrorBuffer);
            DisposeHelper.Dispose(ref _mirrorBlurBuffer);
            DisposeHelper.Dispose(ref _temporaryBuffer);
            DisposeHelper.Dispose(ref _mirrorDepthBuffer);

            DisposeHelper.Dispose(ref _sslrBufferScene);
            DisposeHelper.Dispose(ref _sslrBufferResult);
            DisposeHelper.Dispose(ref _sslrBufferBaseReflection);
            DisposeHelper.Dispose(ref _gBufferNormals);
            DisposeHelper.Dispose(ref _gBufferDepthD);
            DisposeHelper.Dispose(ref _gBufferDepthAlt);
            DisposeHelper.Dispose(ref _aoBuffer);

            base.DisposeOverride();
        }
    }
}