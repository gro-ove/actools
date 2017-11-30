using System;
using System.Collections.Generic;
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
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
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
        private TargetResourceTexture _mirrorBuffer, _mirrorBlurBuffer, _mirrorTemporaryBuffer;
        private TargetResourceDepthTexture _mirrorDepthBuffer;

        private void UpdateMirrorBuffers() {
            var newState = FlatMirror && FlatMirrorBlurred ? 2 : !AnyGround && FlatMirror ? 1 : 0;
            var previousState = _mirrorBuffer == null ? 0 : _mirrorBlurBuffer == null ? 1 : 2;
            if (newState == previousState) return;

            if (newState != 0) {
                if (previousState == 0) {
                    _mirrorBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _mirrorDepthBuffer = TargetResourceDepthTexture.Create();
                }

                if (newState == 2) {
                    _mirrorBlurBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                    _mirrorTemporaryBuffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                } else {
                    DisposeHelper.Dispose(ref _mirrorBlurBuffer);
                    DisposeHelper.Dispose(ref _mirrorTemporaryBuffer);
                }

                if (previousState < newState) {
                    if (!InitiallyResized) return;
                    ResizeMirrorBuffers();
                }
            } else {
                DisposeHelper.Dispose(ref _mirrorBuffer);
                DisposeHelper.Dispose(ref _mirrorBlurBuffer);
                DisposeHelper.Dispose(ref _mirrorTemporaryBuffer);
                DisposeHelper.Dispose(ref _mirrorDepthBuffer);
            }
        }

        protected override void ClearRenderTarget() {
            var color = (Color4)BackgroundColor * BackgroundBrightness;
            color.Alpha = 0f;
            DeviceContext.ClearRenderTargetView(InnerBuffer.TargetView, color);
        }

        private void ResizeMirrorBuffers() {
            if (DeviceContextHolder == null) return;
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
            get => base.ShowWireframe;
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

            // Use private field because main property is affected by ShowroomNode value as well
            if (_useCorrectAmbientShadows) {
                RecreateAoBuffer();
                UpdateGBuffers();
                OnPropertyChanged(nameof(UseCorrectAmbientShadows));
            }

            if (ShowroomNode == null) {
                CarNode?.StopMovement();
                CarNode?.ResetPosition();
            }

            OnShowroomChangedLights();
        }

        private void RecreateAoBuffer() {
            if (!UseAo && !UseCorrectAmbientShadows) {
                DisposeHelper.Dispose(ref _aoBuffer);
                _effect?.FxUseAo.Set(false);
                return;
            }

            var format = AoType.GetFormat();
            _aoHelper = null;
            if (_aoBuffer == null || _aoBuffer.Format != format) {
                DisposeHelper.Dispose(ref _aoBuffer);
                _aoBuffer = TargetResourceTexture.Create(format);
            }

            if (InitiallyResized) {
                ResizeAoBuffer();
            }
        }

        private void ResizeAoBuffer() {
            if (DeviceContextHolder == null) return;
            _aoBuffer?.Resize(DeviceContextHolder, Width, Height, null);
        }

        private bool _gBuffersReady;
        private TargetResourceTexture _gBufferNormals, _gBufferDepthAlt;
        private TargetResourceDepthTexture _gBufferDepthD;

        protected bool GMode() {
            return UseSslr || UseAo || UseDof && !AccumulationMode || UseCorrectAmbientShadows;
        }

        private void UpdateGBuffers() {
            var value = GMode();
            if (_gBufferNormals != null == value) return;

            if (value) {
                _gBufferNormals = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                _gBufferDepthAlt = TargetResourceTexture.Create(Format.R32_Float);
                _gBufferDepthD = TargetResourceDepthTexture.Create();
                if (InitiallyResized) {
                    ResizeGBuffers();
                }
                _gBuffersReady = true;
            } else {
                DisposeHelper.Dispose(ref _gBufferNormals);
                DisposeHelper.Dispose(ref _gBufferDepthAlt);
                DisposeHelper.Dispose(ref _gBufferDepthD);
                _gBuffersReady = false;
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
            AllowSkinnedObjects = true;

            if (showroomKn5 != null) {
                _showroom = true;
            }

            BackgroundColor = Color.FromArgb(220, 220, 220);
            BackgroundBrightness = showroomKn5 == null ? 1f : 2f;
            EnableShadows = EffectDarkMaterial.EnableShadows;

            InitializeLights();
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

        private FlatMirror _mirror;
        private RenderableList _carWrapper;

        private void RecreateFlatMirror() {
            if (_carWrapper == null) return;

            var replaceMode = _carWrapper.ElementAtOrDefault(0) is FlatMirror;
            if (replaceMode) {
                _carWrapper[0].Dispose();
                _carWrapper.RemoveAt(0);
            }

            DisposeHelper.Dispose(ref _mirror);
            var mirrorPlane = new Plane(Vector3.Zero, Vector3.UnitY);
            _mirror = FlatMirror
                    ? new FlatMirror(
                            new RenderableList("_cars", Matrix.Identity,
                                    CarSlots.Select(x => x.CarNode).Concat(Lights.Select(x => x.Enabled ? x.GetRenderableObject() : null)).NonNull()),
                            mirrorPlane, !AnyGround)
                    : new FlatMirror(mirrorPlane, OpaqueGround, !AnyGround);
            if (FlatMirror && ShowWireframe) {
                _mirror.SetInvertedRasterizerState(DeviceContextHolder.States.WireframeInvertedState);
            }

            _carWrapper.Insert(0, _mirror);

            if (replaceMode) {
                _carWrapper.UpdateBoundingBox();
            }
        }

        protected override void ExtendCar([NotNull] CarSlot slot, [CanBeNull] Kn5RenderableCar car, [NotNull] RenderableList carWrapper) {
            base.ExtendCar(slot, car, carWrapper);
            OnCarChangedLights(slot, car);

            _carWrapper = carWrapper;
            _mirrorDirty = true;

            if (_meshDebug) {
                UpdateMeshDebug(car);
            }
        }

        private bool _mirrorDirty;

        protected override void OnCarObjectsChanged() {
            base.OnCarObjectsChanged();
            SetShadowsDirty();
            _mirrorDirty = true;
        }

        protected override void PrepareCamera(CameraBase camera) {
            base.PrepareCamera(camera);

            if (camera is CameraOrbit orbit) {
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
            return new ReflectionCubemap(CubemapReflectionMapSize);
        }

        [NotNull]
        private EffectDarkMaterial Effect => _effect ?? (_effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>());
        private EffectDarkMaterial _effect;

        private Vector3 _light;

        private int? GetShadowsNumSplits() {
            // Several cars — turn on all cascades!
            if (CarSlots.Length > 1) return 3;

            // Just a car — single cascade
            if (ShowroomNode == null) return 1;

            // Showroom doesn’t cast shadows — single cascade
            if (ShowroomNode.Meshes.All(x => !x.OriginalNode.CastShadows)) return 1;

            // Showroom casts shadows all over the scene
            if (ShowroomNode.OriginalFile.Materials.Values.All(x =>
                    x.GetPropertyByName("ksDiffuse")?.ValueA == 0f && x.GetPropertyByName("ksAmbient")?.ValueA >= 1f)) return null;

            return 3;
        }

        private int _numSplits;

        protected override void UpdateShadows(ShadowsDirectional shadows, Vector3 center) {
            _pcssParamsSet = false;

            var splitsNum = GetShadowsNumSplits();
            if (splitsNum == null) {
                // Everything is shadowed
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

                UpdateReflectedLightShadowSize(splits[0]);
            }

            InvalidateLightsShadows();
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

        private bool _effectNoiseMapSet;

        public void SetEffectNoiseMap() {
            if (!_effectNoiseMapSet) {
                _effectNoiseMapSet = true;
                Effect.FxNoiseMap.SetResource(DeviceContextHolder.GetRandomTexture(16, 16));
            }
        }

        private bool _pcssParamsSet;

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
            SetEffectNoiseMap();
        }

        protected override void DrawPrepare(Vector3 eyesPosition, Vector3 light) {
            UpdateCarLights();
            UpdateEffect();
            base.DrawPrepare(eyesPosition, light);
        }

        protected override void SetReflectionCubemapDirty() {
            base.SetReflectionCubemapDirty();
            for (var i = 0; i < _lightProbes.Count; i++) {
                _lightProbes[i].SetDirty();
            }
        }

        private readonly List<LightProbe> _lightProbes = new List<LightProbe>();

        private readonly LazierFn<float, float> _cubemapReflectionOffset =
                new LazierFn<float, float>(v => (float)((Math.Log(v, 2) - 11) / 8.0));

        protected override void InitializeInner() {
            base.InitializeInner();
            for (var i = 0; i < _lightProbes.Count; i++) {
                _lightProbes[i].Initialize(DeviceContextHolder);
            }
        }

        private void PrepareLightProbes() {
            if (CubemapAmbient <= 0f) return;

            var slots = CarSlots;
            var slotsCount = CarSlots.Length;
            var shs = _lightProbes;

            if (shs.Count != slotsCount) {
                while (shs.Count < slotsCount) {
                    var sh = new LightProbe();
                    sh.Initialize(DeviceContextHolder);
                    shs.Add(sh);
                }

                for (var i = shs.Count - 1; i >= slotsCount; i--) {
                    shs[i].Dispose();
                    shs.RemoveAt(i);
                }
            }

            var shotAll = ShotDrawInProcess || ForceUpdateWholeCubemapAtOnce;
            var shotAny = false;
            for (var i = 0; i < slotsCount; i++) {
                var sh = shs[i];
                var center = slots[i].GetCarBoundingBox()?.GetCenter();
                if (center.HasValue) {
                    sh.Update(center.Value);
                    sh.BackgroundColor = (Color4)BackgroundColor * BackgroundBrightness;
                    if (!sh.IsDirty) continue;

                    if (shotAny) {
                        // Something already was shot in this frame, wait for the next…
                        IsDirty = true;

                        // No point checking other light probes:
                        break;
                    }

                    sh.DrawScene(DeviceContextHolder, this, shotAll ? 6 : CubemapReflectionFacesPerFrame);
                    if (!shotAll && sh.IsDirty) {
                        // Wait for next frame
                        IsDirty = true;

                        // No point checking other light probes:
                        break;
                    }

                    shotAny = !shotAll;
                }
            }
        }

        protected override void DrawPrepareEffect(Vector3 eyesPosition, Vector3 light, ShadowsDirectional shadows, ReflectionCubemap reflection,
                bool singleLight) {
            if (reflection != null) {
                PrepareLightProbes();
            }

            var effect = Effect;
            effect.FxEyePosW.Set(eyesPosition);
            effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));

            // For lighted reflection later
            _light = light;

            // Simlified lighting
            effect.FxLightDir.Set(light);
            effect.FxLightColor.Set(LightColor.ToVector3() * LightBrightness);

            if (_complexMode) {
                // Complex lighting
                UpdateLights(light, shadows != null, singleLight);
            } else {
                _mainLight.Direction = light;
                _mainLight.Brightness = LightBrightness;
                _mainLight.Color = LightColor;
            }

            // Reflections
            effect.FxReflectionPower.Set(MaterialsReflectiveness);
            effect.FxCubemapReflections.Set(IsCubemapReflectionActive);

            if (IsCubemapReflectionActive) {
                effect.FxCubemapReflectionsOffset.Set(_cubemapReflectionOffset.Get(CubemapReflectionMapSize));
            }

            // Shadows
            var useShadows = EnableShadows && LightBrightness > 0f && shadows != null;
            effect.FxNumSplits.Set(useShadows ? _numSplits : 0);

            if (useShadows) {
                effect.FxPcssEnabled.Set(UsePcss);
                if (UsePcss) {
                    PreparePcss(shadows);
                }
            }

            // Colors
            effect.FxAmbientDown.Set(AmbientDown.ToVector3() * AmbientBrightness);
            effect.FxAmbientRange.Set((AmbientUp.ToVector3() - AmbientDown.ToVector3()) * AmbientBrightness);
            effect.FxBackgroundColor.Set(BackgroundColor.ToVector3() * BackgroundBrightness);

            // Flat mirror
            if (FlatMirror && ShowroomNode == null) {
                effect.FxFlatMirrorPower.Set(FlatMirrorReflectiveness);
            }

            effect.FxReflectionCubemap.SetResource(reflection?.View);
            effect.FxCubemapAmbient.Set(reflection == null ? 0f : FxCubemapAmbientValue);

#if DEBUG
            var debugReflections = DeviceContextHolder.GetEffect<EffectSpecialDebugReflections>();
            debugReflections.FxEyePosW.Set(eyesPosition);
            debugReflections.FxReflectionCubemap.SetResource(reflection?.View);
#endif
        }

        protected override void DrawCars(DeviceContextHolder holder, ICamera camera, SpecialRenderMode mode) {
            var sh = _lightProbes;
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                if (i < sh.Count) {
                    _effect.FxLightProbe.Set(sh[i].Values);
                }

                CarSlots[i].CarNode?.Draw(holder, camera, mode);
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

        private void SetMirrorMode() {
            if (!AnyGround) {
                _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.ShadowOnlyGround);
            } else if (!FlatMirror) {
                _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.BackgroundGround);
            } else if (FlatMirrorBlurred) {
                _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.TextureMirror);
            } else {
                _mirror.SetMode(DeviceContextHolder, FlatMirrorMode.TransparentMirror);
            }
        }

        protected override void DrawScene() {
            var effect = Effect;
            var showroomNode = ShowroomNode;

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = GetRasterizerState();

            // Draw reflection if needed
            if (showroomNode == null && FlatMirror && _mirror != null) {
                effect.FxLightDir.Set(new Vector3(_light.X, -_light.Y, _light.Z));
                SetFlatMirrorSide(-1);

                if (_complexMode) {
                    DarkLightBase.FlipPreviousY(effect, _lights, _lights.Length);
                }

                if (FlatMirrorBlurred) {
                    DeviceContext.ClearDepthStencilView(_mirrorDepthBuffer.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                    var bg = (Color4)BackgroundColor * BackgroundBrightness;
                    bg.Alpha = 0f;
                    DeviceContext.ClearRenderTargetView(_mirrorBuffer.TargetView, bg);
                    DeviceContext.ClearRenderTargetView(_mirrorBlurBuffer.TargetView, default(Color4));
                    DeviceContext.ClearRenderTargetView(_mirrorTemporaryBuffer.TargetView, default(Color4));
                    DeviceContext.OutputMerger.SetTargets(_mirrorDepthBuffer.DepthView, _mirrorBuffer.TargetView);
                    DrawMirror();

                    DeviceContext.Rasterizer.SetViewports(OutputViewport);
                    var mult = FlatMirrorBlurMuiltiplier.Clamp(0.1f, 3f);
                    DeviceContextHolder.GetHelper<BlurHelper>().BlurFlatMirror(DeviceContextHolder, _mirrorBuffer, _mirrorTemporaryBuffer,
                            ActualCamera.ViewProjInvert, _mirrorDepthBuffer.View, 60f * mult, target: _mirrorBlurBuffer);
                    DeviceContextHolder.GetHelper<BlurHelper>().BlurFlatMirror(DeviceContextHolder, _mirrorBlurBuffer, _mirrorTemporaryBuffer,
                            ActualCamera.ViewProjInvert, _mirrorDepthBuffer.View, 12f * mult);
                    DeviceContext.Rasterizer.SetViewports(Viewport);

                    DeviceContext.OutputMerger.SetTargets(DepthStencilView, InnerBuffer.TargetView);
                } else if (AnyGround) {
                    DrawMirror();
                } else {
                    DeviceContext.ClearDepthStencilView(_mirrorDepthBuffer.DepthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                    var bg = (Color4)BackgroundColor * BackgroundBrightness;
                    bg.Alpha = 0f;
                    DeviceContext.ClearRenderTargetView(_mirrorBuffer.TargetView, bg);
                    DeviceContext.OutputMerger.SetTargets(_mirrorDepthBuffer.DepthView, _mirrorBuffer.TargetView);
                    DrawMirror();
                    DeviceContext.OutputMerger.SetTargets(DepthStencilView, InnerBuffer.TargetView);
                }

                effect.FxLightDir.Set(_light);

                if (_complexMode) {
                    DarkLightBase.FlipPreviousY(effect, _lights, _lights.Length);
                }
            }

            // Draw a scene, apart from car
            if (showroomNode != null) {
                SetFlatMirrorSide(0);

                if (CubemapAmbient != 0f) {
                    effect.FxCubemapAmbient.Set(0f);
                }

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
                showroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

                DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
                showroomNode.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

                if (CubemapAmbient != 0f) {
                    effect.FxCubemapAmbient.Set(FxCubemapAmbientValue);
                }
            } else {
                // Draw a mirror
                if (_mirror != null) {
                    SetFlatMirrorSide(-1);

                    SetMirrorMode();
                    if (AnyGround) {
                        if (!FlatMirror) {
                            _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);
                        } else if (FlatMirrorBlurred && _mirrorBuffer != null) {
                            _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple, _mirrorBlurBuffer.View, null, null);
                        } else {
                            _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);
                        }
                    } else if (FlatMirror) {
                        _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple,
                                FlatMirrorBlurred ? _mirrorBlurBuffer.View : _mirrorBuffer.View, null, null);
                    } else {
                        effect.FxFlatMirrorPower.Set(0f);
                        _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple, null, null, null);
                    }

                    SetFlatMirrorSide(1);
                }
            }

            // Shadows
            if (!UseCorrectAmbientShadows) {
                effect.FxAmbientShadowOpacity.Set(CarShadowsOpacity);
                for (var i = CarSlots.Length - 1; i >= 0; i--) {
                    CarSlots[i].CarNode?.DrawAmbientShadows(DeviceContextHolder, ActualCamera);
                }
            }

            // Visible area lights
            if (_areaLightsMode) {
                DrawLights(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);
            }

            // Car itself
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.LessEqualDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.ReadOnlyDepthState;
            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.SimpleTransparent);

            // Debug stuff
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                CarSlots[i].CarNode?.DrawDebug(DeviceContextHolder, ActualCamera);
            }

            SetFlatMirrorSide(0);

            if (ShowMovementArrows) {
                for (var i = CarSlots.Length - 1; i >= 0; i--) {
                    CarSlots[i].CarNode?.DrawMovementArrows(DeviceContextHolder, Camera);
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

        [CanBeNull]
        private BlurHelper _blurHelper;

        protected void DrawPreparedSceneToBuffer() {
            ClearRenderTarget();

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

        private EffectPpAmbientShadows _aoShadowEffect;

        // Do not dispose it! it’s just a temporary value from DrawSceneToBuffer()
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

        private int _flatMirrorSide;
        private void SetFlatMirrorSide(int side) {
            if (ActualCamera.Position.Y <= 0f) side = 0;
            if (_flatMirrorSide == side) return;

            _flatMirrorSide = side;
            Effect.FxFlatMirrorSide.Set(side);
            if (_areaLightsMode) {
                DeviceContextHolder.GetEffect<EffectSpecialAreaLights>().FxFlatMirrorSide.Set(side);
            }
        }

        protected override void DrawSceneToBuffer() {
            if (!_gBuffersReady) {
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
            var msaaMode = UseMsaa && GBufferMsaa;

            if (msaaMode) {
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
                SetFlatMirrorSide(0);
            } else if (_mirror != null) {
                SetFlatMirrorSide(-1);

                var effect = Effect;

                SetMirrorMode();
                if (FlatMirror && !FlatMirrorBlurred) {
                    _mirror.DrawReflection(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

                    var reflectionBuffer = _sslr?.BufferBaseReflection;
                    if (reflectionBuffer != null) {
                        DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, reflectionBuffer.View, _sslr.BufferResult.TargetView);
                        effect.FxDiffuseMap.SetResource(_sslr.BufferResult.View);
                        DeviceContext.OutputMerger.SetTargets(reflectionBuffer.TargetView);
                        _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);

                        if (msaaMode) {
                            DeviceContext.OutputMerger.SetTargets(DepthStencilView, reflectionBuffer.TargetView, _gBufferNormals.TargetView,
                                    _gBufferDepthAlt.TargetView);
                        } else {
                            DeviceContext.OutputMerger.SetTargets(_gBufferDepthD.DepthView, reflectionBuffer.TargetView, _gBufferNormals.TargetView);
                        }
                    }
                } else {
                    _mirror.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
                }

                SetFlatMirrorSide(1);
            } else {
                SetFlatMirrorSide(0);
            }

            DrawCars(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
            if (_areaLightsMode) {
                DrawLights(DeviceContextHolder, ActualCamera, SpecialRenderMode.GBuffer);
            }

            SetFlatMirrorSide(0);

            if (ShowDepth) {
                DeviceContextHolder.GetHelper<CopyHelper>().DepthToLinear(DeviceContextHolder, _lastDepthBuffer, InnerBuffer.TargetView,
                        Camera.NearZValue, Camera.FarZValue, (Camera.Position - MainSlot.CarCenter).Length() * 2);
                return;
            }

            if (UseAo || UseCorrectAmbientShadows) {
                var aoHelper = _aoHelper ?? (_aoHelper = AoType.GetHelper(DeviceContextHolder));

                if (UseAo) {
                    var aoRadius = AoRadius;
                    if (AoType.IsScreenSpace()) {
                        aoRadius *= (float)(ShotInProcess ? ShotResolutionMultiplier : ResolutionMultiplier);
                    }

                    aoHelper.Draw(DeviceContextHolder, _lastDepthBuffer, _gBufferNormals.View, ActualCamera, _aoBuffer.TargetView,
                            AoOpacity, aoRadius, UseDof && UseAccumulationDof && !_realTimeAccumulationFirstStep || _accumulationDofShotInProcess);
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
                    _aoShadowEffect.FxShadowOpacity.Set(CarShadowsOpacity);

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
                effect.FxUseAo.Set(true);

                if (AoDebug) {
                    if (_aoBuffer?.Format == Format.R8_UNorm) {
                        DeviceContextHolder.GetHelper<CopyHelper>().DrawFromRed(DeviceContextHolder, _aoBuffer.View, InnerBuffer.TargetView);
                    } else {
                        DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _aoBuffer.View, InnerBuffer.TargetView);
                    }
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
        private bool _realTimeAccumulationFirstStep;
        private int _realTimeAccumulationSize;

        public override int AccumulatedFrame => _realTimeAccumulationSize;

        private TargetResourceTexture _accumulationTexture, _accumulationMaxTexture,
                _accumulationTemporaryTexture, _accumulationBaseTexture;

        private void DrawRealTimeDofAccumulation() {
            if (_realTimeAccumulationSize >= AccumulationDofIterations) {
                foreach (var slot in CarSlots) {
                    slot.CarNode?.UpdateSound(DeviceContextHolder, Camera);
                }

                PrepareForFinalPass();
                var copy = DeviceContextHolder.GetHelper<CopyHelper>();

                if (_accumulationDofBokeh) {
                    var between = PpBetweenBuffer;
                    copy.AccumulateDivide(DeviceContextHolder, _accumulationTexture.View, between.TargetView, _realTimeAccumulationSize);

                    var bufferAColorGrading = PpColorGradingBuffer;
                    if (!UseColorGrading || bufferAColorGrading == null) {
                        HdrPass(between.View, RenderTargetView, OutputViewport);
                    } else {
                        var hdrView = HdrPass(between.View, bufferAColorGrading.TargetView, bufferAColorGrading.Viewport) ?? bufferAColorGrading.View;
                        ColorGradingPass(hdrView, RenderTargetView, OutputViewport);
                    }
                } else {
                    copy.AccumulateDivide(DeviceContextHolder, _accumulationTexture.View, RenderTargetView, _realTimeAccumulationSize);
                }

                return;
            }

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
            _realTimeAccumulationFirstStep = _realTimeAccumulationSize == 0;
            _realTimeAccumulationSize++;

            if (_realTimeAccumulationFirstStep) {
                DeviceContext.ClearRenderTargetView(_accumulationTexture.TargetView, default(Color4));
                DrawSceneToBuffer();
            } else {
                using (ReplaceCamera(GetDofAccumulationCamera(Camera, (_realTimeAccumulationSize / 50f).Saturate()))) {
                    DrawSceneToBuffer();
                }
            }

            var bufferF = InnerBuffer;
            if (bufferF == null) return;

            bool? originalFxaa = null;
            if (_realTimeAccumulationFirstStep) {
                originalFxaa = UseFxaa;
                UseFxaa = true;
                IsDirty = false;
            }

            if (accumulationDofBokeh) {
                var result = AaPass(bufferF.View, _accumulationTemporaryTexture.TargetView) ?? _accumulationTemporaryTexture.View;
                var copy = DeviceContextHolder.GetHelper<CopyHelper>();

                if (_realTimeAccumulationFirstStep) {
                    UseFxaa = originalFxaa ?? true;
                    IsDirty = false;
                    copy.Draw(DeviceContextHolder, result, _accumulationBaseTexture.TargetView);
                }

                DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddState;
                copy.DrawSqr(DeviceContextHolder, result, _accumulationTexture.TargetView);
                DeviceContext.OutputMerger.BlendState = null;

                var between = PpBetweenBuffer;
                if (_realTimeAccumulationSize < 4) {
                    copy.Draw(DeviceContextHolder, _accumulationBaseTexture.View, between.TargetView);
                } else {
                    copy.AccumulateDivide(DeviceContextHolder, _accumulationTexture.View, between.TargetView, _realTimeAccumulationSize);
                }

                var bufferAColorGrading = PpColorGradingBuffer;
                if (!UseColorGrading || bufferAColorGrading == null) {
                    HdrPass(between.View, RenderTargetView, OutputViewport);
                } else {
                    var hdrView = HdrPass(between.View, bufferAColorGrading.TargetView, bufferAColorGrading.Viewport) ?? bufferAColorGrading.View;
                    ColorGradingPass(hdrView, RenderTargetView, OutputViewport);
                }
            } else {
                var result = AaThenBloom(bufferF.View, _accumulationTemporaryTexture.TargetView) ?? _accumulationTemporaryTexture.View;
                var copy = DeviceContextHolder.GetHelper<CopyHelper>();

                if (_realTimeAccumulationFirstStep) {
                    UseFxaa = originalFxaa ?? true;
                    IsDirty = false;
                    copy.Draw(DeviceContextHolder, result, _accumulationBaseTexture.TargetView);
                }

                DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.AddState;
                copy.DrawSqr(DeviceContextHolder, result, _accumulationTexture.TargetView);
                DeviceContext.OutputMerger.BlendState = null;

                if (_realTimeAccumulationSize < 4) {
                    copy.Draw(DeviceContextHolder, _accumulationBaseTexture.View, RenderTargetView);
                } else {
                    copy.AccumulateDivide(DeviceContextHolder, _accumulationTexture.View, RenderTargetView, _realTimeAccumulationSize);
                }
            }
        }

        private void DrawDof() {
            DrawSceneToBuffer();

            var bufferF = InnerBuffer;
            if (bufferF == null) return;

            _dof.FocusPlane = DofFocusPlane;
            _dof.DofCoCScale = DofScale * (ShotDrawInProcess ? 6f * (ActualWidth / 960f).Clamp(1f, 2f) * Width / ActualWidth : 6f);
            _dof.DofCoCLimit = ShotDrawInProcess ? 64f : 24f;
            _dof.MaxSize = ShotDrawInProcess ? 1920 : 960;
            _dof.Prepare(DeviceContextHolder, ActualWidth, ActualHeight);

            var result = AaThenBloom(bufferF.View, _dof.BufferScene.TargetView) ?? _dof.BufferScene.View;
            _dof.Process(DeviceContextHolder, _lastDepthBuffer, result, ActualCamera, RenderTargetView, false);
        }

        protected override void DrawOverride() {
            if (!UseDof || _dof == null) {
                base.DrawOverride();
            } else if (_realTimeAccumulationMode) {
                DrawRealTimeDofAccumulation();
            } else {
                DrawDof();
            }
        }

        private bool _setCameraHigher = true;

        public bool SetCameraHigher {
            get => _setCameraHigher;
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
            _lightProbes.DisposeEverything();

            DisposeLights();
            base.DisposeOverride();
        }
    }
}