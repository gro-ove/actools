using System;
using System.Collections.Generic;
using System.Drawing;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public abstract class DarkLightBase : IMoveable, IDisposable {
        public bool Enabled { get; set; } = true;

        public bool IsMovable { get; set; } = true;

        #region Movement
        private MoveableHelper _movable;

        public MoveableHelper Movable => _movable ?? (_movable = CreateMoveableHelper());

        protected virtual MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.All);
        }

        public void DrawMovementArrows(DeviceContextHolder holder, BaseCamera camera) {
            if (!IsMovable) return;
            Movable.ParentMatrix = Matrix.Translation(Position);
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        void IMoveable.Move(Vector3 delta) {
            if (!IsMovable) return;
            Position = Position + delta;
        }

        public abstract void Rotate(Quaternion delta);
        #endregion

        public Vector3 Position = new Vector3(0f, 2f, 0f);
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

        public abstract void DrawDummy(IDeviceContextHolder holder, ICamera camera);

        public abstract void Dispose();
    }
}