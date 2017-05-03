using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public abstract class DarkLightBase : IDisposable {
        public bool Enabled { get; set; } = true;

        public Color Color = Color.White;
        public float Brightness = 1f;
        public DarkShadowsMode ShadowsMode;
        public int ShadowsResolution = 1024;

        private int _shadowsOffset;

        protected abstract void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw);

        public void Update(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw) {
            // main shadows are rendered separately, by DarkKn5ObjectRenderer so all those
            // splits/PCSS/etc will be handled properly
            if (Enabled && ShadowsMode != DarkShadowsMode.Off && ShadowsMode != DarkShadowsMode.Main) {
                UpdateShadowsOverride(holder, shadowsPosition, shadowsDraw);
            }
        }

        protected abstract void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar);

        protected virtual void SetOverride(ref EffectDarkMaterial.Light light) {
            light.Color = Color.ToVector3() * Brightness;
            light.ShadowMode = _shadowsOffset == -1 ? 0 : (uint)((uint)ShadowsMode + _shadowsOffset);
        }

        private static readonly EffectDarkMaterial.Light[] LightsBuffer = new EffectDarkMaterial.Light[EffectDarkMaterial.MaxLighsAmount];
        private static readonly float[] ExtraShadowsSizesBuffer = new float[EffectDarkMaterial.MaxExtraShadows];
        private static readonly Vector4[] ExtraShadowsNearFarBuffer = new Vector4[EffectDarkMaterial.MaxExtraShadows];
        private static readonly Matrix[] ExtraShadowsMatricesBuffer = new Matrix[EffectDarkMaterial.MaxExtraShadows];
        private static readonly ShaderResourceView[] ExtraShadowsViewsBuffer = new ShaderResourceView[EffectDarkMaterial.MaxExtraShadows];

        public static void FlipPreviousY(EffectDarkMaterial effect) {
            for (var i = 0; i < LightsBuffer.Length; i++) {
                LightsBuffer[i].DirectionW.Y *= -1f;
                LightsBuffer[i].PosW.Y *= -1f;
            }

            effect.FxLights.SetArray(LightsBuffer);
        }

        public static void ToShader(EffectDarkMaterial effect, IReadOnlyList<DarkLightBase> lights, int count) {
            var shadowOffset = 0;

            for (var i = count - 1; i >= 0; i--) {
                var l = lights[i];
                if (l.Enabled && l.ShadowsMode != DarkShadowsMode.Off && l.ShadowsMode != DarkShadowsMode.Main) {
                    lights[i]._shadowsOffset = -1;
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.Enabled && l.ShadowsMode == DarkShadowsMode.ExtraSmooth) {
                    var offset = shadowOffset++;
                    l._shadowsOffset = offset;

                    if (offset >= EffectDarkMaterial.MaxExtraShadows) {
                        l.ShadowsMode = DarkShadowsMode.Off;
                    } else {
                        l.SetShadowOverride(out ExtraShadowsSizesBuffer[offset], out ExtraShadowsMatricesBuffer[offset],
                                out ExtraShadowsViewsBuffer[offset], ref ExtraShadowsNearFarBuffer[offset]);
                        if (offset >= EffectDarkMaterial.MaxExtraShadowsSmooth) {
                            l.ShadowsMode = DarkShadowsMode.ExtraFast;
                        }
                    }
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.Enabled && (l.ShadowsMode == DarkShadowsMode.ExtraFast || l.ShadowsMode == DarkShadowsMode.ExtraPoint) && l._shadowsOffset == -1) {
                    var offset = shadowOffset++;
                    l._shadowsOffset = offset;

                    if (offset >= EffectDarkMaterial.MaxExtraShadows) {
                        l.ShadowsMode = DarkShadowsMode.Off;
                    } else {
                        l.SetShadowOverride(out ExtraShadowsSizesBuffer[offset], out ExtraShadowsMatricesBuffer[offset],
                                out ExtraShadowsViewsBuffer[offset], ref ExtraShadowsNearFarBuffer[offset]);
                    }
                }
            }

            var j = 0;
            for (var i = 0; i < count && j < EffectDarkMaterial.MaxLighsAmount; i++) {
                var l = lights[i];
                if (l.Enabled) {
                    l.SetOverride(ref LightsBuffer[j++]);
                }
            }

            for (; j < EffectDarkMaterial.MaxLighsAmount; j++) {
                LightsBuffer[j].Type = EffectDarkMaterial.LightOff;
            }

            effect.FxLights.SetArray(LightsBuffer);

            if (effect.FxExtraShadowMapSize != null) {
                effect.FxExtraShadowMapSize.Set(ExtraShadowsSizesBuffer);
                effect.FxExtraShadowViewProj.SetMatrixArray(ExtraShadowsMatricesBuffer);
                effect.FxExtraShadowMaps.SetResourceArray(ExtraShadowsViewsBuffer);
                effect.FxExtraShadowCubeMaps.SetResourceArray(ExtraShadowsViewsBuffer);
                effect.FxExtraShadowNearFar.Set(ExtraShadowsNearFarBuffer);
            }
        }

        public abstract void InvalidateShadows();

        public abstract void Dispose();
    }

    public enum DarkShadowsMode : uint {
        Off = EffectDarkMaterial.LightShadowOff,
        Main = EffectDarkMaterial.LightShadowMain,
        ExtraSmooth = EffectDarkMaterial.LightShadowExtra,
        ExtraFast = EffectDarkMaterial.LightShadowExtraFast,
        ExtraPoint = EffectDarkMaterial.LightShadowExtraCube,
    }

    public class DarkPointLight : DarkLightBase {
        public Vector3 Position = new Vector3(1f, 1f, 1f);
        public float Range = 2f;
        private ShadowsPoint _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsPoint(ShadowsResolution);
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(Position, Range)) {
                    _shadows.DrawScene(holder, shadowsDraw);
                }
            } else if (!_shadowsCleared) {
                _shadowsCleared = true;
                _shadows.Clear(holder);
            }
        }

        protected override void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            if (_shadows == null) {
                size = 0;
                matrix = Matrix.Identity;
                view = null;
            } else {
                size = 1f / _shadows.MapSize;
                matrix = Matrix.Identity;
                view = _shadows.View;

                var n = _shadows.NearZValue;
                var f = _shadows.FarZValue;
                nearFar.X = n;
                nearFar.Y = f;
                nearFar.Z = (f + n) / (f - n);
                nearFar.W = 2f * f * n / (f - n);
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.PosW = Position;
            light.Range = Range;
            light.Type = EffectDarkMaterial.LightPoint;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _shadows);
        }
    }

    public class DarkDirectionalLight : DarkLightBase {
        public Vector3 Direction = new Vector3(-1f, -1f, -1f);
        private ShadowsDirectional _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsDirectional(ShadowsResolution, new[] { 50f });
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(-Direction, shadowsPosition)) {
                    _shadows.DrawScene(holder, shadowsDraw);
                }
            } else if (!_shadowsCleared) {
                _shadowsCleared = true;
                _shadows.Clear(holder);
            }
        }

        protected override void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            if (_shadows == null) {
                size = 0;
                matrix = Matrix.Identity;
                view = null;
            } else {
                size = 1f / _shadows.MapSize;
                matrix = _shadows.Splits[0].ShadowTransform;
                view = _shadows.Splits[0].View;
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.DirectionW = Vector3.Normalize(Direction);
            light.Type = EffectDarkMaterial.LightDirectional;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _shadows);
        }
    }

    public class DarkSpotLight : DarkLightBase {
        public Vector3 Direction = new Vector3(-1f, -1f, -1f);
        public Vector3 Position = new Vector3(0f, 2f, 0f);
        public float Angle = MathF.PI / 6;
        public float SpotFocus = 0.5f;
        public float Range = 2f;
        private ShadowsSpot _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsSpot(ShadowsResolution);
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(Position, -Direction, Range, Angle * 2f)) {
                    _shadows.DrawScene(holder, shadowsDraw);
                }
            } else if (!_shadowsCleared) {
                _shadowsCleared = true;
                _shadows.Clear(holder);
            }
        }

        protected override void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            if (_shadows == null) {
                size = 0;
                matrix = Matrix.Identity;
                view = null;
            } else {
                size = 1f / _shadows.MapSize;
                matrix = _shadows.ShadowTransform;
                view = _shadows.View;
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.DirectionW = Vector3.Normalize(Direction);
            light.PosW = Position;
            light.Range = Range;
            light.SpotlightCosMin = Angle.Cos();
            light.SpotlightCosMax = light.SpotlightCosMin.Lerp(1f, SpotFocus);
            //light.SpotlightAngle = Angle;
            /*light.LightParams.X = Range;
            light.LightParams.Y = Angle.Cos();
            light.LightParams.Z = light.LightParams.Y.Lerp(1f, SpotFocus);*/
            //light.LightParams = new Vector4(Range, Angle.Cos(), Angle.Cos().Lerp(1f, SpotFocus), 0f);
            light.Type = EffectDarkMaterial.LightSpot;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _shadows);
        }
    }
}