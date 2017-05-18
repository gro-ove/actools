using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkPointLight : DarkLightBase {
        public DarkPointLight() : base(DarkLightType.Point) {
            HighQualityShadowsAvailable = true;
        }

        protected override DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            switch (newType) {
                case DarkLightType.Point:
                    return new DarkPointLight {
                        Range = Range
                    };
                case DarkLightType.Directional:
                    return new DarkDirectionalLight();
                case DarkLightType.Spot:
                    return new DarkSpotLight {
                        Range = Range
                    };
                case DarkLightType.Sphere:
                    return new DarkAreaSphereLight {
                        Range = Range
                    };
                case DarkLightType.Tube:
                    return new DarkAreaTubeLight {
                        Range = Range
                    };
                case DarkLightType.Plane:
                    return new DarkAreaPlaneLight {
                        Range = Range
                    };
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["range"] = Range;
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
        }

        private float _range = 2f;

        public float Range {
            get { return _range; }
            set {
                if (value.Equals(_range)) return;
                _range = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private ShadowsPointFlat _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsPointFlat(ShadowsResolution, EffectDarkMaterial.CubemapPadding);
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;

                if (ShadowsResolution > 2048) {
                    ShadowsResolution = 2048;
                }

                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(ActualPosition, Range)) {
                    _shadows.DrawScene(holder, shadowsDraw);
                }
            } else if (!_shadowsCleared) {
                _shadowsCleared = true;
                _shadows.Clear(holder);
            }
        }

        protected override void SetShadowOverride(out Vector4 size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            if (_shadows == null) {
                size = default(Vector4);
                matrix = Matrix.Identity;
                view = null;
            } else {
                size = new Vector4(_shadows.MapSize, _shadows.MapSize * 6,
                        ShadowsBlurMultiplier / _shadows.MapSize, ShadowsBlurMultiplier / 6f / _shadows.MapSize);
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

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.Range = Range;
            light.Type = EffectDarkMaterial.LightPoint;
            light.Flags |= EffectDarkMaterial.LightShadowsCube;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _shadows);
            DisposeHelper.Dispose(ref _dummy);
        }

        public override void Rotate(Quaternion delta) {}

        protected override MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.None);
        }

        private RenderableList _dummy;

        public override void DrawDummy(IDeviceContextHolder holder, ICamera camera) {
            if (_dummy == null) {
                _dummy = new RenderableList {
                    DebugLinesObject.GetLinesSphere(Matrix.Identity, Vector3.UnitY, new Color4(1f, 1f, 1f, 0f), 20, 0.04f)
                };
            }
            
            _dummy.ParentMatrix = ActualPosition.ToFixedSizeMatrix(camera);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}