using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
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

        private TargetResourceTexture _mirrorBuffer, _mirrorBlurBuffer, _mirrorTemporaryBuffer;
        private TargetResourceDepthTexture _mirrorDepthBuffer;

        private void UpdateBlurredFlatMirror() {
            var use = FlatMirror && FlatMirrorBlurred;
            if (use == (_mirrorBuffer != null)) return;

            if (use) {
                _mirrorBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _mirrorBlurBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _mirrorTemporaryBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _mirrorDepthBuffer = TargetResourceDepthTexture.Create();

                if (!InitiallyResized) return;
                ResizeMirrorBuffers();
            } else {
                DisposeHelper.Dispose(ref _mirrorBuffer);
                DisposeHelper.Dispose(ref _mirrorBlurBuffer);
                DisposeHelper.Dispose(ref _mirrorTemporaryBuffer);
                DisposeHelper.Dispose(ref _mirrorDepthBuffer);
            }
        }

        private void ResizeMirrorBuffers() {
            _mirrorBuffer?.Resize(DeviceContextHolder, Width, Height, null);
            _mirrorDepthBuffer?.Resize(DeviceContextHolder, Width, Height, null);
            _mirrorBlurBuffer?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            _mirrorTemporaryBuffer?.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
        }

        protected override void ResizeInner() {
            base.ResizeInner();
            ResizeMirrorBuffers();
            ResizeAoBuffer();
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

        private bool _gBufferMsaa = true;

        public bool GBufferMsaa {
            get { return _gBufferMsaa; }
            set {
                if (value == _gBufferMsaa) return;
                _gBufferMsaa = value;
                OnPropertyChanged();
            }
        }

        private bool _useSslr;
        private DarkSslr _sslr;

        public bool UseSslr {
            get { return _useSslr; }
            set {
                if (Equals(value, _useSslr)) return;
                _useSslr = value;
                IsDirty = true;
                OnPropertyChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _sslr);
                } else if (_sslr == null) {
                    _sslr = new DarkSslr();
                }

                UpdateGBuffers();
            }
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

                RecreateAoBuffer();
                UpdateGBuffers();
            }
        }

        private bool _useCorrectAmbientShadows;

        public bool UseCorrectAmbientShadows {
            get { return _useCorrectAmbientShadows && ShowroomNode != null; }
            set {
                if (Equals(value, _useCorrectAmbientShadows)) return;
                _useCorrectAmbientShadows = value;
                IsDirty = true;
                OnPropertyChanged();

                RecreateAoBuffer();
                UpdateGBuffers();
            }
        }

        protected override void OnShowroomChanged() {
            base.OnShowroomChanged();

            if (UseCorrectAmbientShadows) {
                RecreateAoBuffer();
                UpdateGBuffers();
                OnPropertyChanged(nameof(UseCorrectAmbientShadows));
            }
        }

        private bool _blurCorrectAmbientShadows;

        public bool BlurCorrectAmbientShadows {
            get { return _blurCorrectAmbientShadows; }
            set {
                if (Equals(value, _blurCorrectAmbientShadows)) return;
                _blurCorrectAmbientShadows = value;
                OnPropertyChanged();
                IsDirty = true;
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
            if (!UseAo && !UseCorrectAmbientShadows) {
                DisposeHelper.Dispose(ref _aoBuffer);
                _effect?.FxUseAo.Set(false);
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
                ResizeAoBuffer();
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

        private void ResizeAoBuffer() {
            _aoBuffer?.Resize(DeviceContextHolder, Width, Height, null);
        }

        private TargetResourceTexture _gBufferNormals, _gBufferDepthAlt;
        private TargetResourceDepthTexture _gBufferDepthD;

        protected virtual bool GMode() {
            return UseSslr || UseAo || UseDof || UseCorrectAmbientShadows;
        }

        private void UpdateGBuffers() {
            var value = GMode();
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

            if (UseAo || UseCorrectAmbientShadows) {
                Effect.FxUseAo.Set(false);
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
                                           .BlurFlatMirror(DeviceContextHolder, _mirrorBlurBuffer, _mirrorTemporaryBuffer, ActualCamera.ViewProjInvert,
                                                   _mirrorDepthBuffer.View, 60f);
                    } else {
                        DeviceContextHolder.GetHelper<BlurHelper>()
                                           .BlurFlatMirror(DeviceContextHolder, _mirrorBuffer, _mirrorTemporaryBuffer, ActualCamera.ViewProjInvert,
                                                   _mirrorDepthBuffer.View, 60f, target: _mirrorBlurBuffer);
                    }

                    DeviceContextHolder.GetHelper<BlurHelper>()
                                       .BlurFlatMirror(DeviceContextHolder, _mirrorBlurBuffer, _mirrorTemporaryBuffer, ActualCamera.ViewProjInvert,
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
            if (!UseCorrectAmbientShadows) {
                carNode.DrawAmbientShadows(DeviceContextHolder, ActualCamera);
            }

            // car itself
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            carNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            carNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

            // debug stuff
            carNode.DrawDebug(DeviceContextHolder, ActualCamera);

            if (ShowMovementArrows) {
                carNode.DrawMovementArrows(DeviceContextHolder, Camera,
                        new Vector2(MousePosition.X / ActualWidth, MousePosition.Y / ActualHeight));
            }
        }

        private bool _showDepth;

        public bool ShowDepth {
            get { return _showDepth; }
            set {
                if (Equals(value, _showDepth)) return;
                _showDepth = value;
                OnPropertyChanged();
            }
        }

        protected override string GetInformationString() {
            var aa = new[] {
                UseMsaa ? MsaaSampleCount + "xMSAA" : null,
                UseSsaa ? $"{Math.Pow(ResolutionMultiplier, 2d).Round()}xSSAA" : null,
                UseFxaa ? "FXAA" : null,
            }.NonNull().JoinToString(", ");

            var se = new[] {
                UseDof ? UseAccumulationDof ? "Acc. DOF" : "DOF" : null,
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
Color: {(string.IsNullOrWhiteSpace(pp) ? "Original" : pp)}
Skin editing: {(ImageUtils.IsMagickSupported ? MagickOverride ? "Magick.NET av., enabled" : "Magick.NET av., disabled" : "Magick.NET not available")}".Trim();
        }

        [CanBeNull]
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

        private bool _useDof;
        private DarkDof _dof;

        public bool UseDof {
            get { return _useDof; }
            set {
                if (Equals(value, _useDof)) return;
                _useDof = value;
                OnPropertyChanged();

                if (!value) {
                    DisposeHelper.Dispose(ref _dof);
                } else if (_dof == null) {
                    _dof = new DarkDof();
                }

                IsDirty = true;
                UpdateGBuffers();
            }
        }

        private float _dofFocusPlane = 1.6f;

        public float DofFocusPlane {
            get { return _dofFocusPlane; }
            set {
                if (Equals(value, _dofFocusPlane)) return;
                _dofFocusPlane = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _dofScale = 1f;

        public float DofScale {
            get { return _dofScale; }
            set {
                if (Equals(value, _dofScale)) return;
                _dofScale = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private EffectPpAmbientShadows aoShadowEffect;

        // do not dispose it! it’s just a temporary value from DrawSceneToBuffer() 
        // to DrawOverride() allowing to apply DOF after AA/HDR/color grading/bloom stages
        private ShaderResourceView _lastDepthBuffer;

        protected override void DrawSceneToBuffer() {
            if (!GMode()) {
                base.DrawSceneToBuffer();
                return;
            }

            DrawPrepare();

            if (UseSslr) {
                _sslr.Prepare(DeviceContextHolder, GBufferMsaa);
            }

            if (_blurHelper == null) {
                _blurHelper = DeviceContextHolder.GetHelper<BlurHelper>();
            }

            // Draw scene to G-buffer to get normals, depth and base reflection
            DeviceContext.Rasterizer.SetViewports(Viewport);

            var sample = GBufferMsaa ? SampleDescription : (SampleDescription?)null;
            if (UseMsaa && GBufferMsaa) {
                _gBufferDepthAlt.Resize(DeviceContextHolder, Width, Height, sample);
                DeviceContext.OutputMerger.SetTargets(DepthStencilView, _sslr?.BufferBaseReflection.TargetView, _gBufferNormals.TargetView,
                        _gBufferDepthAlt.TargetView);
                DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                DeviceContext.ClearRenderTargetView(_gBufferDepthAlt.TargetView, (Color4)new Vector4(1f));
                _lastDepthBuffer = _gBufferDepthAlt.View;
            } else {
                _gBufferDepthD.Resize(DeviceContextHolder, Width, Height, sample);
                DeviceContext.OutputMerger.SetTargets(_gBufferDepthD.DepthView, _sslr?.BufferBaseReflection.TargetView, _gBufferNormals.TargetView);
                DeviceContext.ClearDepthStencilView(_gBufferDepthD.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                _lastDepthBuffer = _gBufferDepthD.View;
            }

            DeviceContext.ClearRenderTargetView(_gBufferNormals.TargetView, (Color4)new Vector4(0.5f));
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

            var c = CarNode;
            c?.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

            if (ShowDepth) {
                DeviceContextHolder.GetHelper<CopyHelper>().DepthToLinear(DeviceContextHolder, _lastDepthBuffer, InnerBuffer.TargetView,
                        Camera.NearZValue, Camera.FarZValue, (Camera.Position - CarCenter).Length() * 2);
                return;
            }
            
            if (UseAo || UseCorrectAmbientShadows) {
                var aoHelper = _aoHelper;
                if (aoHelper == null) {
                    aoHelper = _aoHelper = GetAoHelper();
                }

                /*if (AoType == AoType.Hbao) {
                    UseSslr = true;
                    SetInnerBuffer(_sslrBufferScene);
                    DrawPreparedSceneToBuffer();
                    (aoHelper as HbaoHelper)?.Prepare(DeviceContextHolder, _sslrBufferScene.View);
                    SetInnerBuffer(null);
                }*/

                if (UseAo) {
                    aoHelper.Draw(DeviceContextHolder, _lastDepthBuffer, _gBufferNormals.View, ActualCamera, _aoBuffer.TargetView,
                            AoOpacity);
                    aoHelper.Blur(DeviceContextHolder, _aoBuffer, InnerBuffer, Camera);
                } else {
                    DeviceContext.ClearRenderTargetView(_aoBuffer.TargetView, new Color4(1f, 1f, 1f, 1f));
                }

                if (UseCorrectAmbientShadows) {
                    if (aoShadowEffect == null) {
                        aoShadowEffect = DeviceContextHolder.GetEffect<EffectPpAmbientShadows>();
                        aoShadowEffect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(4, 4));
                    }

                    aoShadowEffect.FxDepthMap.SetResource(_lastDepthBuffer);

                    DeviceContext.OutputMerger.SetTargets(_aoBuffer.TargetView);
                    DeviceContextHolder.PrepareQuad(aoShadowEffect.LayoutPT);
                    DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.MultiplyState;

                    aoShadowEffect.FxViewProj.SetMatrix(Camera.ViewProj);
                    aoShadowEffect.FxViewProjInv.SetMatrix(Camera.ViewProjInvert);

                    if (BlurCorrectAmbientShadows) {
                        aoShadowEffect.FxNoiseSize.Set(new Vector2(Width / 4f, Height / 4f));
                    }

                    if (c != null) {
                        var s = c.GetAmbientShadows();
                        for (var i = 0; i < s.Count; i++) {
                            var o = s[i] as AmbientShadow;
                            var v = o == null ? null : c.GetAmbientShadowView(DeviceContextHolder, o);
                            if (v == null) continue;

                            aoShadowEffect.FxShadowMap.SetResource(v);

                            var m = o.Transform * o.ParentMatrix;
                            if (!o.BoundingBox.HasValue) {
                                o.UpdateBoundingBox();
                                if (!o.BoundingBox.HasValue) continue;
                            }
                            var b = o.BoundingBox.Value.GetSize();

                            aoShadowEffect.FxShadowPosition.Set(m.GetTranslationVector());
                            aoShadowEffect.FxShadowSize.Set(new Vector2(1f / b.X, 1f / b.Z));
                            aoShadowEffect.FxShadowViewProj.SetMatrix(Matrix.Invert(m) * new Matrix {
                                M11 = 0.5f,
                                M22 = 0.5f,
                                M33 = 0.5f,
                                M41 = 0.5f,
                                M42 = 0.5f,
                                M43 = 0.5f,
                                M44 = 1f
                            });

                            if (BlurCorrectAmbientShadows) {
                                aoShadowEffect.TechAddShadowBlur.DrawAllPasses(DeviceContext, 6);
                            } else {
                                aoShadowEffect.TechAddShadow.DrawAllPasses(DeviceContext, 6);
                            }
                        }
                    }

                    DeviceContext.OutputMerger.BlendState = null;
                }

                var effect = Effect;
                effect.FxAoMap.SetResource(_aoBuffer.View);
                Effect.FxUseAo.Set(true);
                effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

                if (AoDebug) {
                    DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _aoBuffer.View, InnerBuffer.TargetView);
                    return;
                }
            }

            if (UseSslr && _sslr != null) {
                // Draw actual scene to _sslrBufferScene
                SetInnerBuffer(_sslr.BufferScene);
                DrawPreparedSceneToBuffer();
                SetInnerBuffer(null);
                _sslr?.Process(DeviceContextHolder, _lastDepthBuffer, _gBufferNormals.View, ActualCamera,
                        (float)ResolutionMultiplier, InnerBuffer, InnerBuffer.TargetView);
            } else {
                DrawPreparedSceneToBuffer();
            }
        }

        private bool _realTimeAccumulationMode;
        private int _realTimeAccumulationSize;

        private TargetResourceTexture _accumulationTexture, _accumulationMaxTexture,
                _accumulationTemporaryTexture, _accumulationBaseTexture;

        private void DrawRealTimeDofAccumulation() {
            if (_accumulationTexture == null) {
                _accumulationTexture = TargetResourceTexture.Create(Format.R32G32B32A32_Float);
                _accumulationMaxTexture = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                _accumulationBaseTexture = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
                _accumulationTemporaryTexture = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            }

            if (_accumulationTexture.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null)) {
                _accumulationTemporaryTexture.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
                _accumulationBaseTexture.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            }

            var accumulationDofBokeh = AccumulationDofBokeh;
            if (accumulationDofBokeh) {
                _accumulationMaxTexture.Resize(DeviceContextHolder, ActualWidth / 2, ActualHeight / 2, null);
            }

            var firstStep = _realTimeAccumulationSize == 0;
            _realTimeAccumulationSize++;

            if (firstStep) {
                DeviceContext.ClearRenderTargetView(_accumulationTexture.TargetView, default(Color4));
                if (accumulationDofBokeh) {
                    DeviceContext.ClearRenderTargetView(_accumulationMaxTexture.TargetView, default(Color4));
                }
                DrawSceneToBuffer();
            } else {
                using (ReplaceCamera(GetDofAccumulationCamera(Camera, (_realTimeAccumulationSize / 50f).Saturate()))) {
                    DrawSceneToBuffer();
                }
            }

            var bufferF = InnerBuffer;
            if (bufferF == null) return;

            var result = AaThenBloom(bufferF.View, _accumulationTemporaryTexture.TargetView) ?? _accumulationTemporaryTexture.View;
            var copy = DeviceContextHolder.GetHelper<CopyHelper>();

            if (firstStep) {
                copy.Draw(DeviceContextHolder, result, _accumulationBaseTexture.TargetView);
            }

            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddState;
            copy.Draw(DeviceContextHolder, result, _accumulationTexture.TargetView);

            if (accumulationDofBokeh) {
                DeviceContext.Rasterizer.SetViewports(_accumulationMaxTexture.Viewport);
                DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.MaxState;
                copy.Draw(DeviceContextHolder, result, _accumulationMaxTexture.TargetView);
                DeviceContext.Rasterizer.SetViewports(OutputViewport);
            }

            DeviceContext.OutputMerger.BlendState = null;

            if (_realTimeAccumulationSize < 4) {
                copy.Draw(DeviceContextHolder, _accumulationBaseTexture.View, RenderTargetView);
            } else if (accumulationDofBokeh) {
                copy.AccumulateBokehDivide(DeviceContextHolder, _accumulationTexture.View, _accumulationMaxTexture.View, RenderTargetView,
                        _realTimeAccumulationSize, 0.5f);
            } else {
                copy.AccumulateDivide(DeviceContextHolder, _accumulationTexture.View, RenderTargetView, _realTimeAccumulationSize);
            }
        }

        private void DrawDof() {
            DrawSceneToBuffer();

            var bufferF = InnerBuffer;
            if (bufferF == null) return;

            _dof.FocusPlane = DofFocusPlane;
            _dof.DofCoCScale = DofScale * (ShotInProcess ? 12f : 6f);
            _dof.DofCoCLimit = ShotInProcess ? 64f : 24f;
            _dof.MaxSize = ShotInProcess ? 1920 : 960;
            _dof.Prepare(DeviceContextHolder, ActualWidth, ActualHeight);

            var result = AaThenBloom(bufferF.View, _dof.BufferScene.TargetView) ?? _dof.BufferScene.View;
            _dof.Process(DeviceContextHolder, _lastDepthBuffer, result, ActualCamera, RenderTargetView, false);
        }

        protected override void DrawOverride() {
           if (!UseDof || _dof == null || _lastDepthBuffer == null) {
                base.DrawOverride();
            } else if (_realTimeAccumulationMode) {
                DrawRealTimeDofAccumulation();
            } else { 
                DrawDof();
            }
        }

        #region Accumulation DOF
        private bool _useAccumulationDof;

        public bool UseAccumulationDof {
            get { return _useAccumulationDof; }
            set {
                if (Equals(value, _useAccumulationDof)) return;
                _useAccumulationDof = value;
                IsDirty = true;
                OnPropertyChanged();
            }
        }

        private int _accumulationDofIterations = 100;

        public int AccumulationDofIterations {
            get { return _accumulationDofIterations; }
            set {
                if (Equals(value, _accumulationDofIterations)) return;
                _accumulationDofIterations = value;
                OnPropertyChanged();
            }
        }

        private float _accumulationDofApertureSize = 0.02f;

        public float AccumulationDofApertureSize {
            get { return _accumulationDofApertureSize; }
            set {
                if (Equals(value, _accumulationDofApertureSize)) return;
                _accumulationDofApertureSize = value;
                IsDirty = true;
                OnPropertyChanged();
                _realTimeAccumulationSize = 0;
            }
        }

        private bool _accumulationDofBokeh;

        public bool AccumulationDofBokeh {
            get { return _accumulationDofBokeh; }
            set {
                if (Equals(value, _accumulationDofBokeh)) return;
                _accumulationDofBokeh = value;
                OnPropertyChanged();
                _realTimeAccumulationSize = 0;
            }
        }

        protected override bool CanShotWithoutExtraTextures => base.CanShotWithoutExtraTextures && (!UseDof || !UseAccumulationDof);

        private BaseCamera GetDofAccumulationCamera(BaseCamera camera, float apertureMultipler) {
            Vector2 direction;
            do {
                direction = new Vector2(MathUtils.Random(-1f, 1f), MathUtils.Random(-1f, 1f));
            } while (direction.LengthSquared() > 1f);

            var bokeh = camera.Right * direction.X + camera.Up * direction.Y;
            var newCamera = new FpsCamera(camera.FovY);
            var newPosition = camera.Position + AccumulationDofApertureSize * apertureMultipler * bokeh;
            var lookAt = camera.Position + camera.Look * DofFocusPlane;
            newCamera.LookAt(newPosition, lookAt, camera.Up);
            newCamera.SetLens(AspectRatio);
            newCamera.UpdateViewMatrix();
            return newCamera;
        }

        private IDisposable ReplaceCamera(BaseCamera newCamera) {
            var camera = Camera;
            Camera = newCamera;
            return new ActionAsDisposable(() => {
                Camera = camera;
            });
        }

        protected override void DrawShot(RenderTargetView target, IProgress<double> progress, CancellationToken cancellation) {
            if (UseDof && UseAccumulationDof && target != null) {
                var copy = DeviceContextHolder.GetHelper<CopyHelper>();
                _useDof = false;

                using (var summary = TargetResourceTexture.Create(Format.R32G32B32A32_Float))
                using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                    summary.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
                    temporary.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);

                    var iterations = AccumulationDofIterations;
                    for (var i = 0; i < iterations; i++) {
                        if (cancellation.IsCancellationRequested) return;

                        Vector2 direction;
                        do {
                            direction = new Vector2(MathUtils.Random(-1f, 1f), MathUtils.Random(-1f, 1f));
                        } while (direction.LengthSquared() > 1f);

                        using (ReplaceCamera(GetDofAccumulationCamera(Camera, 1f))) {
                            progress?.Report(0.05 + 0.9 * i / iterations);
                            base.DrawShot(temporary.TargetView, progress, cancellation);
                        }

                        DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddState;
                        copy.Draw(DeviceContextHolder, temporary.View, summary.TargetView);
                        DeviceContext.OutputMerger.BlendState = null;
                    }

                    copy.AccumulateDivide(DeviceContextHolder, summary.View, target, iterations);
                }
                
                _useDof = true;
                return;
            }

            base.DrawShot(target, progress, cancellation);
        }

        public override bool AccumulationMode => UseDof && UseAccumulationDof;

        protected override void OnTick(float dt) {
            base.OnTick(dt);
            if (IsDirty) {
                _realTimeAccumulationSize = 0;
            }
        }

        public override void Draw() {
            if (UseDof && UseAccumulationDof) {
                _realTimeAccumulationMode = true;
                if (IsDirty) {
                    _realTimeAccumulationSize = 0;
                }
                
                base.Draw();
            } else {
                if (_realTimeAccumulationMode) {
                    DisposeHelper.Dispose(ref _accumulationTexture);
                    DisposeHelper.Dispose(ref _accumulationMaxTexture);
                    DisposeHelper.Dispose(ref _accumulationTemporaryTexture);
                    DisposeHelper.Dispose(ref _accumulationBaseTexture);
                    _realTimeAccumulationSize = 0;
                    _realTimeAccumulationMode = false;
                }

                base.Draw();
            }
        }
        #endregion

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
            DisposeHelper.Dispose(ref _sslr);
            DisposeHelper.Dispose(ref _dof);

            DisposeHelper.Dispose(ref _mirror);
            DisposeHelper.Dispose(ref _mirrorBuffer);
            DisposeHelper.Dispose(ref _mirrorBlurBuffer);
            DisposeHelper.Dispose(ref _mirrorTemporaryBuffer);
            DisposeHelper.Dispose(ref _mirrorDepthBuffer);

            DisposeHelper.Dispose(ref _gBufferNormals);
            DisposeHelper.Dispose(ref _gBufferDepthD);
            DisposeHelper.Dispose(ref _gBufferDepthAlt);
            DisposeHelper.Dispose(ref _aoBuffer);

            DisposeHelper.Dispose(ref _accumulationTexture);
            DisposeHelper.Dispose(ref _accumulationMaxTexture);
            DisposeHelper.Dispose(ref _accumulationTemporaryTexture);
            DisposeHelper.Dispose(ref _accumulationBaseTexture);

            base.DisposeOverride();
        }

        public void AutoFocus(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(ActualWidth, ActualHeight));
            var distance = Scene.SelectManyRecursive(x => x as RenderableList)
                                .OfType<IKn5RenderableObject>()
                                .Where(x => x.IsInitialized)
                                .Select(node => {
                                    var f = node.CheckIntersection(ray);
                                    return f.HasValue ? new {
                                        Node = node,
                                        Distance = f.Value
                                    } : null;
                                })
                                .Where(x => x != null)
                                .MinEntry(x => x.Distance)?.Distance;
            if (distance.HasValue) {
                DofFocusPlane = distance.Value;
            }
        }
    }
}