using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer : ToolsKn5ObjectRenderer {
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

        public override bool ShowWireframe {
            get { return base.ShowWireframe; }
            set {
                base.ShowWireframe = value;
                (_carWrapper?.ElementAtOrDefault(0) as FlatMirror)?.SetInvertedRasterizerState(
                        value ? DeviceContextHolder.States.WireframeInvertedState : null);
            }
        }

        private TargetResourceTexture _aoBuffer;

        [CanBeNull]
        private AoHelperBase _aoHelper;

        protected override void OnShowroomChanged() {
            base.OnShowroomChanged();

            if (UseCorrectAmbientShadows) {
                RecreateAoBuffer();
                UpdateGBuffers();
                OnPropertyChanged(nameof(UseCorrectAmbientShadows));
            }

            if (ShowroomNode == null) {
                CarNode?.Movable.StopMovement();
                CarNode?.ResetPosition();
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
        private readonly DarkDirectionalLight _mainLight, _reflectedLight;

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

            _mainLight = new DarkDirectionalLight {
                Tag = DarkLightTag.Main,
                DisplayName = "Sun",
                Position = Vector3.UnitY - Vector3.UnitZ,
                IsMainLightSource = true,
                IsVisibleInUi = false,
                IsMovable = false
            };

            _reflectedLight = new DarkDirectionalLight {
                Tag = DarkLightTag.Main,
                DisplayName = "Sun (Reflected)",
                Position = -Vector3.UnitY - Vector3.UnitZ,
                Enabled = false,
                IsMovable = false,
                IsVisibleInUi = false,
                UseShadows = true,
                UseHighQualityShadows = true
            };

            AddLight(_mainLight);
            AddLight(_reflectedLight);
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

        protected override void ExtendCar(CarSlot slot, Kn5RenderableCar car, RenderableList carWrapper) {
            base.ExtendCar(slot, car, carWrapper);

            _car = car;
            if (_car != null) {
                LoadObjLights(DarkLightTag.GetCarTag(slot.Id), _car.RootDirectory);
            }

            _carWrapper = carWrapper;
            _mirrorDirty = true;

            if (_meshDebug) {
                UpdateMeshDebug(car);
            }
        }

        private bool _mirrorDirty;

        protected override void OnCarObjectsChanged() {
            base.OnCarObjectsChanged();
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

            var splitsNum = GetShadowsNumSplits();
            if (splitsNum == null) {
                // everything is shadowed
                _numSplits = -1;
            } else {
                var splits = GetSplits(splitsNum.Value, CarNode?.BoundingBox?.GetSize().Length() ?? 4f);
                shadows.SetSplits(DeviceContextHolder, splits);
                shadows.SetMapSize(DeviceContextHolder, ShadowMapSize);
                base.UpdateShadows(shadows, center);

                _numSplits = splitsNum.Value;

                var effect = Effect;
                effect.FxShadowMapSize.Set(new Vector2(ShadowMapSize, 1f / ShadowMapSize));
                effect.FxShadowMaps.SetResourceArray(shadows.Splits.Take(splitsNum.Value).Select(x => x.View).ToArray());
                effect.FxShadowViewProj.SetMatrixArray(
                        shadows.Splits.Take(splitsNum.Value).Select(x => x.ShadowTransform).ToArray());

                _reflectedLight.ShadowsSize = splits[0];
            }

            if (_complexMode) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    _lights[i].InvalidateShadows();
                }
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
            DrawCars(holder, camera, SpecialRenderMode.Shadow);

            if (FlatMirrorReflectedLight && ShowroomNode == null && FlatMirror && !FlatMirrorBlurred) {
                _mirror.MirroredObject?.Draw(holder, camera, SpecialRenderMode.Shadow);
            }
        }

        public override void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            var showroomNode = ShowroomNode;
            if (showroomNode == null) return;

            if (UseAo || UseCorrectAmbientShadows) {
                Effect.FxUseAo.Set(false);
            }

            DrawPrepareEffect(camera.Position, Light, ReflectionsWithShadows ? _shadows : null, null, !ReflectionsWithMultipleLights);
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.InvertedState;
            showroomNode.Draw(holder, camera, SpecialRenderMode.Reflection);
            DeviceContext.Rasterizer.State = null;
        }

        private float FxCubemapAmbientValue => CubemapAmbientWhite ? -CubemapAmbient : CubemapAmbient;

        private bool _pcssNoiseMapSet, _pcssParamsSet;

        private void PreparePcss(ShadowsDirectional shadows) {
            if (_pcssParamsSet) return;
            _pcssParamsSet = true;

            var effect = Effect;
            if (!effect.FxNoiseMap.IsValid) return;

            var splits = new Vector4[shadows.Splits.Length];
            var sceneScale = (ShowroomNode == null ? 1f : 2f) * PcssSceneScale;
            var lightScale = PcssLightScale;
            for (var i = 0; i < shadows.Splits.Length; i++) {
                splits[i] = new Vector4(sceneScale / shadows.Splits[i].Size, lightScale / shadows.Splits[i].Size, 0, 0);
            }

            effect.FxPcssScale.Set(splits);

            if (!_pcssNoiseMapSet) {
                _pcssNoiseMapSet = true;
                effect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(16, 16));
            }
        }

        private class MovingLight {
            public MovingLight(IDeviceContextHolder holder) {
                var color = new Vector3(MathUtils.Random(1f), MathUtils.Random(1f),
                        MathUtils.Random(1f));
                color.Normalize();

                /*Light = new DarkSpotLight {
                    Tag = DarkLightTag.Main,
                    UseShadows = true,
                    Color = color.ToDrawingColor(),
                    Range = 10f,
                    Angle = 0.5f,
                    Brightness = 0.5f,
                    SpotFocus = 0.75f,
                    IsMovable = false
                };*/

                Light = new DarkSpotLight {
                    Tag = DarkLightTag.Main,
                    UseShadows = true,
                    UseHighQualityShadows = true,
                    ShadowsResolution = 2048,
                    Color = color.ToDrawingColor(),
                    Range = 20f,
                    Angle = 0.5f,
                    Brightness = 3.5f,
                    SpotFocus = 0.75f,
                    IsMovable = false
                };

                _a = MathUtils.Random(2f, 3f);
                _b = MathUtils.Random(1f);
                _c = MathUtils.Random(5f, 6f);
                _d = MathUtils.Random(1f);
                _e = MathUtils.Random(1f);
                _f = MathUtils.Random(1f);
                _g = MathUtils.Random(3f, 4f);

                _stopwatch = holder.StartNewStopwatch();
            }

            public readonly DarkLightBase Light;
            private readonly RendererStopwatch _stopwatch;
            private readonly float _a, _b, _c, _d, _e, _f, _g;

            public bool Update() {
                if (!Light.Enabled) return false;

                var elapsed = (float)_stopwatch.ElapsedSeconds;
                Light.Position = new Vector3(
                        (elapsed * _b + _d).Sin() * _c, _a,
                        (elapsed * _e + _f).Sin() * _g);

                var spot = Light as DarkSpotLight;
                if (spot != null) {
                    spot.Direction = spot.Position;
                }

                return true;
            }
        }

        [NotNull]
        private DarkLightBase[] _lights = new DarkLightBase[0];

        [NotNull]
        public DarkLightBase[] Lights {
            get { return _lights; }
            set {
                if (Equals(value, _lights)) return;

                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (!value.Contains(light)) {
                        light.Dispose();
                        light.PropertyChanged -= OnLightPropertyChanged;
                    }
                }

                for (var i = value.Length - 1; i >= 0; i--) {
                    var light = value[i];
                    if (!_lights.Contains(light)) {
                        light.PropertyChanged += OnLightPropertyChanged;
                    }
                }

                _lights = value;
                OnPropertyChanged();
                IsDirty = true;
                SetReflectionCubemapDirty();
            }
        }

        private void OnLightPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(DarkLightBase.Type):
                    var light = (DarkLightBase)sender;
                    RemoveLight(light);

                    if (light.Tag == DarkLightTag.Extra) {
                        AddLight(light.ChangeType(light.Type));
                    } else {
                        var index = _lights.FindIndex(x => x.Tag > light.Tag);
                        if (index == -1) {
                            AddLight(light.ChangeType(light.Type));
                        } else {
                            InsertLightAt(light.ChangeType(light.Type), index);
                        }
                    }
                    break;
                case nameof(DarkLightBase.IsDeleted):
                    RemoveLight((DarkLightBase)sender);
                    break;
                default:
                    if (!Disposed) {
                        LightPropertyChanged?.Invoke(sender, e);
                    }
                    break;
            }
        }

        public event PropertyChangedEventHandler LightPropertyChanged;

        private void PrepareNewLight(DarkLightBase light) {
            if (light.DisplayName == null) {
                for (var i = 1; i < 999; i++) {
                    var c = "Light #" + i;
                    if (_lights.All(x => x.DisplayName != c)) {
                        light.DisplayName = c;
                        break;
                    }
                }
            }
        }

        public void AddLight(DarkLightBase light) {
            PrepareNewLight(light);
            var updated = new DarkLightBase[_lights.Length + 1];
            Array.Copy(_lights, updated, _lights.Length);
            updated[updated.Length - 1] = light;
            Lights = updated;
        }

        public void InsertLightAt(DarkLightBase light, int index) {
            PrepareNewLight(light);
            var updated = new DarkLightBase[_lights.Length + 1];
            Array.Copy(_lights, updated, index);
            Array.Copy(_lights, index, updated, index + 1, _lights.Length - index);
            updated[index] = light;
            Lights = updated;
        }

        public void RemoveLight(DarkLightBase light) {
            var index = _lights.IndexOf(light);
            if (index == -1) return;

            var updated = new DarkLightBase[_lights.Length - 1];
            Array.Copy(_lights, updated, index);
            Array.Copy(_lights, index + 1, updated, index, updated.Length - index);
            Lights = updated;
        }
        
        private readonly List<MovingLight> _movingLights = new List<MovingLight>();
        private readonly Random _random = new Random();

        public void AddLight() {
            var color = new Vector3((float)MathUtils.Random(), (float)MathUtils.Random(),
                    (float)MathUtils.Random());
            color.Normalize();

            /*AddLight(new DarkSpotLight {
                UseShadows = true,
                Color = color.ToDrawingColor(),
                Range = 10f,
                Position = Vector3.UnitY * 2f,
                Direction = Vector3.UnitY,
                Brightness = 0.5f
            });*/
            AddLight(new DarkPointLight {
                UseShadows = true,
                UseHighQualityShadows = true,
                Color = color.ToDrawingColor(),
                Range = 20f,
                Position = Vector3.UnitY * 2f,
                Brightness = 5.5f
            });
        }

        public void RemoveLight() {
            var last = _lights.LastOrDefault(x => x.Tag == DarkLightTag.Extra);
            if (last == null || _movingLights.Any(x => x.Light == last)) return;

            RemoveLight(last);
        }

        public JObject[] SerializeLights(DarkLightTag tag) {
            return Lights.Where(x => x.Tag == tag).Select(x => x.SerializeToJObject()).NonNull().ToArray();
        }

        public void DeserializeLights(DarkLightTag tag, [CanBeNull] IEnumerable<JObject> data) {
            var deserialized = data?.Select(DarkLightBase.Deserialize).NonNull().ToArray() ?? new DarkLightBase[0];
            if (deserialized.Length == 0) return;

            foreach (var light in deserialized) {
                light.Tag = tag;
            }

            // keep the right order
            Lights = Lights.Where(x => x.Tag < tag)
                           .Concat(deserialized)
                           .Concat(Lights.Where(x => x.Tag > tag))
                           .ToArray();
        }

        public void LoadObjLights(DarkLightTag tag, string objDirectory) {
            var filename = Path.Combine(objDirectory, "ui", "cm_lights.json");
            if (!File.Exists(filename)) return;

            var lights = JArray.Parse(File.ReadAllText(filename)).OfType<JObject>();
            DeserializeLights(tag, lights);
        }

        public void AddMovingLight() {
#if DEBUG
            var moving = new MovingLight(DeviceContextHolder);
            AddLight(moving.Light);
            _movingLights.Add(moving);
#endif
        }

        public void RemoveMovingLight() {
#if DEBUG
            var last = _movingLights.LastOrDefault();
            if (last == null) return;

            RemoveLight(last.Light);
            _movingLights.Remove(last);
#endif
        }

        private bool IsFlatMirrorReflectedLightEnabled => FlatMirrorReflectedLight && ShowroomNode == null && FlatMirror && !FlatMirrorBlurred;

        private void UpdateLights(Vector3 mainLightDirection, bool setShadows, bool singleLight) {
            var effect = Effect;

            _mainLight.Direction = mainLightDirection;
            _mainLight.Brightness = LightBrightness;
            _mainLight.Color = LightColor;

            if (IsFlatMirrorReflectedLightEnabled) {
                _reflectedLight.Direction = new Vector3(mainLightDirection.X, -mainLightDirection.Y, mainLightDirection.Z);
                _reflectedLight.Brightness = LightBrightness * FlatMirrorReflectiveness;
                _reflectedLight.Color = LightColor;
                _reflectedLight.ShadowsResolution = ShadowMapSize;
                _reflectedLight.Enabled = true;
            } else {
                _reflectedLight.Enabled = false;
            }

            for (var i = _lights.Length - 1; i >= 0; i--) {
                var l = _lights[i];
                l.Update(DeviceContextHolder, ShadowsPosition, setShadows && !singleLight && EnableShadows ? this : null);
            }

            // TODO: only once?
            if (!_pcssNoiseMapSet) {
                _pcssNoiseMapSet = true;
                effect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(16, 16));
            }

            // TODO: move somewhere else?
            DarkLightBase.ToShader(DeviceContextHolder, effect, _lights, singleLight ? 1 : _lights.Length,
                    _limitedMode ? EffectDarkMaterial.MaxExtraShadows / 2 : EffectDarkMaterial.MaxExtraShadows);
        }

        private static void UpdateCarLights(CarSlot slot, DarkLightBase[] lights) {
            var car = slot.CarNode;
            var tag = DarkLightTag.GetCarTag(slot.Id);
            if (car == null) {
                for (var i = lights.Length - 1; i >= 0; i--) {
                    var l = lights[i];
                    if (l.Tag != tag) continue;

                    l.Enabled = false;
                }
            } else {
                for (var i = lights.Length - 1; i >= 0; i--) {
                    var l = lights[i];
                    if (l.Tag != tag) continue;

                    if (l.ActAsHeadlight) {
                        l.Enabled = car.HeadlightsEnabled;
                        l.BrightnessMultipler = 1f;

                        if (!l.SmoothDelay.HasValue) {
                            l.SmoothDelay = car.GetApproximateHeadlightsDelay() ?? TimeSpan.Zero;
                        }
                    } else if (l.ActAsBrakeLight) {
                        if (l.ActAsDouble == 0f) {
                            l.Enabled = car.BrakeLightsEnabled;
                            l.BrightnessMultipler = 1f;
                        } else {
                            l.Enabled = car.BrakeLightsEnabled || car.HeadlightsEnabled;
                            l.BrightnessMultipler = car.BrakeLightsEnabled ? 1f : l.ActAsDouble;
                        }

                        if (!l.SmoothDelay.HasValue) {
                            l.SmoothDelay = car.GetApproximateBrakeLightsDelay() ?? TimeSpan.Zero;
                        }
                    }

                    if (l.AttachedTo == null) {
                        l.ParentMatrix = car.Matrix;
                    } else {
                        if (l.AttachedToObject == null) {
                            l.AttachedToObject = car.GetByName(l.AttachedTo) ?? (IRenderableObject)car.RootObject;
                            l.AttachedToRelativeMatrix = Matrix.Invert(FindOriginalMatrix(l.AttachedToObject, car.RootObject, Matrix.Identity));
                            if (l.AttachedToObject == null) continue;
                        }

                        var a = l.AttachedToObject as Kn5RenderableList;
                        if (a == null) {
                            l.ParentMatrix = l.AttachedToRelativeMatrix * l.AttachedToObject.ParentMatrix;
                        } else {
                            l.ParentMatrix = l.AttachedToRelativeMatrix * a.Matrix;
                        }
                    }
                }
            }
        }

        private static Matrix FindOriginalMatrix(IRenderableObject obj, RenderableList root, Matrix matrix) {
            while (obj != root && obj != null) {
                matrix *= (obj as Kn5RenderableList)?.OriginalNode.Transform.ToMatrix() ?? (obj as RenderableList)?.LocalMatrix ?? Matrix.Identity;
                obj = obj.GetParent(root);
            }

            return matrix;
        }

        private void UpdateCarLights() {
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                UpdateCarLights(CarSlots[i], _lights);
            }
        }
        
        private bool _complexMode;
        private bool _limitedMode;

        private EffectDarkMaterial.Mode FindAppropriateMode() {
            _complexMode = true;
            _limitedMode = false;
            return EffectDarkMaterial.Mode.Main;

            var useComplex = _lights.Count(x => x.ActuallyEnabled) > 1 || IsFlatMirrorReflectedLightEnabled;
            _complexMode = useComplex;

            if (!EnableShadows) {
                return useComplex ? EffectDarkMaterial.Mode.NoShadows : EffectDarkMaterial.Mode.SimpleNoShadows;
            }

            if (!useComplex) {
                return UsePcss ? EffectDarkMaterial.Mode.Simple : EffectDarkMaterial.Mode.SimpleNoPCSS;
            }

            var shadowsAmount = _lights.Count(x => x.ActuallyEnabled && x.UseShadows);
            _limitedMode = shadowsAmount <= 5;

            return _limitedMode
                    ? (UsePcss ? EffectDarkMaterial.Mode.Limited : EffectDarkMaterial.Mode.LimitedNoPCSS)
                    : (UsePcss ? EffectDarkMaterial.Mode.Main : EffectDarkMaterial.Mode.NoPCSS);
        }

        private void UpdateEffect() {
            if (_effect == null) {
                _effect = DeviceContextHolder.GetExistingEffect<EffectDarkMaterial>();
            }

            var mode = FindAppropriateMode();
            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>(e => e.SetMode(mode, Device));
            } else if (_effect.GetMode() != mode) {
                _effect.SetMode(mode, Device);
                _pcssParamsSet = false;
                _pcssNoiseMapSet = false;
                SetShadowsDirty();
                SetReflectionCubemapDirty();
                // TODO: more?
            }
        }

        protected override void DrawPrepare(Vector3 eyesPosition, Vector3 light) {
            UpdateCarLights();
            UpdateEffect();
            base.DrawPrepare(eyesPosition, light);
        }

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection,
                bool singleLight) {
            var effect = Effect;
            effect.FxEyePosW.Set(eyesPosition);

            // for lighted reflection later
            _light = light;

            // simlified lighting
            effect.FxLightDir.Set(light);
            effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);

            if (_complexMode) {
                // complex lighting
                UpdateLights(light, shadows != null, singleLight);
            } else {
                _mainLight.Direction = light;
                _mainLight.Brightness = LightBrightness;
                _mainLight.Color = LightColor;
            }

            // reflections
            effect.FxReflectionPower.Set(MaterialsReflectiveness);
            effect.FxCubemapReflections.Set(CubemapReflection);
            effect.FxCubemapAmbient.Set(reflection == null ? 0f : FxCubemapAmbientValue);

            // shadows
            var useShadows = EnableShadows && LightBrightness > 0f && shadows != null;
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
            
            effect.FxReflectionCubemap.SetResource(reflection?.View);

#if DEBUG
            var debugReflections = DeviceContextHolder.GetEffect<EffectSpecialDebugReflections>();
            debugReflections.FxEyePosW.Set(eyesPosition);
            debugReflections.FxReflectionCubemap.SetResource(reflection?.View);
#endif
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
            var effect = Effect;

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            // draw reflection if needed
            if (ShowroomNode == null && FlatMirror && _mirror != null) {
                effect.FxLightDir.Set(new Vector3(_light.X, -_light.Y, _light.Z));

                if (_complexMode) {
                    DarkLightBase.FlipPreviousY(effect);
                }

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

                if (_complexMode) {
                    DarkLightBase.FlipPreviousY(effect);
                }
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

            // shadows
            if (!UseCorrectAmbientShadows) {
                for (var i = CarSlots.Length - 1; i >= 0; i--) {
                    CarSlots[i].CarNode?.DrawAmbientShadows(DeviceContextHolder, ActualCamera);
                }
            }

            // car itself
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

            // debug stuff
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                CarSlots[i].CarNode?.DrawDebug(DeviceContextHolder, ActualCamera);
            }

            if (ShowMovementArrows) {
                if (ShowroomNode != null) {
                    for (var i = CarSlots.Length - 1; i >= 0; i--) {
                        CarSlots[i].CarNode?.DrawMovementArrows(DeviceContextHolder, Camera);
                    }
                }

                if (_complexMode) {
                    for (var i = _lights.Length - 1; i >= 0; i--) {
                        var light = _lights[i];
                        if (light.Enabled) {
                            light.DrawDummy(DeviceContextHolder, Camera);
                            if (light.IsMovable) {
                                light.DrawMovementArrows(DeviceContextHolder, Camera);
                            }
                        }
                    }
                }
            }
        }

        protected override bool MoveObjectOverride(Vector2 relativeFrom, Vector2 relativeDelta, BaseCamera camera, bool tryToClone) {
            return ShowroomNode != null && base.MoveObjectOverride(relativeFrom, relativeDelta, camera, tryToClone) ||
                    _complexMode && _lights.Any(light => {
                        IMoveable cloned;
                        if (light.IsMovable && light.Movable.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out cloned)) {
                            var clonedLight = cloned as DarkLightBase;
                            if (clonedLight != null) {
                                InsertLightAt(clonedLight, _lights.IndexOf(light));
                            }
                            return true;
                        }

                        return false;
                    });
        }

        protected override void StopMovementOverride() {
            if (ShowroomNode != null) {
                base.StopMovementOverride();
            }

            if (_complexMode) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (light.Enabled && (light as DarkDirectionalLight)?.IsMainLightSource != true && _movingLights.All(x => x.Light != light)) {
                        light.Movable.StopMovement();
                    }
                }
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

        protected override void DrawSpritesInner() {
            if (_complexMode && ShowMovementArrows) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (light.Enabled) {
                        light.DrawSprites(Sprite, Camera, new Vector2(ActualWidth, ActualHeight));
                    }
                }
            }
            
            base.DrawSpritesInner();
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
Lights: {_lights.Count(x => x.ActuallyEnabled)} (shadows: {(EnableShadows ? 1 + _lights.Count(x => x.ActuallyEnabled && x.UseShadows) : 0)})
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

        private EffectPpAmbientShadows _aoShadowEffect;

        // do not dispose it! it’s just a temporary value from DrawSceneToBuffer() 
        // to DrawOverride() allowing to apply DOF after AA/HDR/color grading/bloom stages
        private ShaderResourceView _lastDepthBuffer;

        private void DrawGBufferAmbientShadows(CarSlot slot) {
            var c = slot.CarNode;
            if (c != null) {
                var s = c.GetAmbientShadows();
                for (var i = 0; i < s.Count; i++) {
                    var o = s[i] as AmbientShadow;
                    var v = o == null ? null : c.GetAmbientShadowView(DeviceContextHolder, o);
                    if (v == null) continue;

                    _aoShadowEffect.FxShadowMap.SetResource(v);

                    var m = o.Transform * o.ParentMatrix;
                    if (!o.BoundingBox.HasValue) {
                        o.UpdateBoundingBox();
                        if (!o.BoundingBox.HasValue) continue;
                    }
                    var b = o.BoundingBox.Value.GetSize();

                    _aoShadowEffect.FxShadowPosition.Set(m.GetTranslationVector());
                    _aoShadowEffect.FxShadowSize.Set(new Vector2(1f / b.X, 1f / b.Z));
                    _aoShadowEffect.FxShadowViewProj.SetMatrix(Matrix.Invert(m) * new Matrix {
                        M11 = -0.5f,
                        M22 = 0.5f,
                        M33 = 0.5f,
                        M41 = 0.5f,
                        M42 = 0.5f,
                        M43 = 0.5f,
                        M44 = 1f,
                    });

                    if (BlurCorrectAmbientShadows) {
                        _aoShadowEffect.TechAddShadowBlur.DrawAllPasses(DeviceContext, 6);
                    } else {
                        _aoShadowEffect.TechAddShadow.DrawAllPasses(DeviceContext, 6);
                    }
                }
            }
        }

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
            
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

            if (ShowDepth) {
                DeviceContextHolder.GetHelper<CopyHelper>().DepthToLinear(DeviceContextHolder, _lastDepthBuffer, InnerBuffer.TargetView,
                        Camera.NearZValue, Camera.FarZValue, (Camera.Position - MainSlot.CarCenter).Length() * 2);
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
                    if (_aoShadowEffect == null) {
                        _aoShadowEffect = DeviceContextHolder.GetEffect<EffectPpAmbientShadows>();
                        _aoShadowEffect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(4, 4));
                    }

                    _aoShadowEffect.FxDepthMap.SetResource(_lastDepthBuffer);

                    DeviceContext.OutputMerger.SetTargets(_aoBuffer.TargetView);
                    DeviceContextHolder.PrepareQuad(_aoShadowEffect.LayoutPT);
                    DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.MultiplyState;

                    _aoShadowEffect.FxViewProj.SetMatrix(Camera.ViewProj);
                    _aoShadowEffect.FxViewProjInv.SetMatrix(Camera.ViewProjInvert);

                    if (BlurCorrectAmbientShadows) {
                        _aoShadowEffect.FxNoiseSize.Set(new Vector2(Width / 4f, Height / 4f));
                    }
                    
                    for (var i = CarSlots.Length - 1; i >= 0; i--) {
                        DrawGBufferAmbientShadows(CarSlots[i]);
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

            foreach (var light in _movingLights) {
                IsDirty |= light.Update();
            }

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
            
            Lights = new DarkLightBase[0]; // thus, disposing everything

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
                                .MinEntryOrDefault(x => x.Distance)?.Distance;
            if (distance.HasValue) {
                DofFocusPlane = distance.Value;
            }
        }
    }
}