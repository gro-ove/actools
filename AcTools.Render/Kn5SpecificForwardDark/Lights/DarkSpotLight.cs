using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Render.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkSpotLight : DarkLightBase {
        public DarkSpotLight() : base(DarkLightType.Spot) {}

        protected override DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            switch (newType) {
                case DarkLightType.Point:
                    return new DarkPointLight {
                        Range = Range
                    };
                case DarkLightType.Directional:
                    return new DarkDirectionalLight {
                        Direction = Direction
                    };
                case DarkLightType.Spot:
                    return new DarkSpotLight {
                        Direction = Direction,
                        Range = Range,
                        Angle = Angle,
                        SpotFocus = SpotFocus
                    };
                case DarkLightType.Plane:
                    return new DarkPlaneLight {
                        Direction = Direction,
                        Range = Range
                    };
                case DarkLightType.AreaSphere:
                    return new DarkAreaSphereLight {
                        Range = Range
                    };
                case DarkLightType.AreaTube:
                    return new DarkAreaTubeLight {
                        Range = Range
                    };
                case DarkLightType.LtcPlane:
                    return new DarkLtcPlaneLight {
                        Direction = Direction,
                        Range = Range
                    };
                case DarkLightType.LtcTube:
                    return new DarkLtcTubeLight {
                        Range = Range
                    };
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["direction"] = VectorToString(Direction);
            obj["range"] = Range;
            obj["angle"] = Angle;
            obj["spot"] = SpotFocus;
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitY;
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Angle = obj["angle"] != null ? (float)obj["angle"] : 0.5f;
            SpotFocus = obj["spot"] != null ? (float)obj["spot"] : 0.5f;
        }

        private Vector3 _direction = new Vector3(-1f, -1f, -1f);

        public Vector3 Direction {
            get => _direction;
            set {
                value = Vector3.Normalize(value);
                if (value.Equals(_direction)) return;
                _direction = value;
                _actualDirection = null;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        protected override void OnParentMatrixChanged() {
            base.OnParentMatrixChanged();
            _actualDirection = null;
        }

        private Vector3? _actualDirection;
        public Vector3 ActualDirection => _actualDirection ?? (_actualDirection = Vector3.TransformNormal(Direction, ParentMatrix)).Value;

        private float _angle = 0.5f;

        public float Angle {
            get => _angle;
            set {
                if (value.Equals(_angle)) return;
                _angle = value;
                ResetDummy();
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private float _spotFocus = 0.5f;

        public float SpotFocus {
            get => _spotFocus;
            set {
                if (value.Equals(_spotFocus)) return;
                _spotFocus = value;
                OnPropertyChanged();
            }
        }

        private float _range = 2f;

        public float Range {
            get => _range;
            set {
                if (value.Equals(_range)) return;
                _range = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

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
                if (_shadows.Update(ActualPosition, ActualDirection, Range, Angle * 2f)) {
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
                size = new Vector4(_shadows.MapSize, _shadows.MapSize,
                        ShadowsBlurMultiplier / _shadows.MapSize, ShadowsBlurMultiplier / _shadows.MapSize);
                matrix = _shadows.ShadowTransform;
                view = _shadows.View;
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.DirectionW = -ActualDirection;
            light.DirectionW.Normalize();
            light.Range = Range;
            light.SpotlightCosMin = Angle.Cos();
            light.SpotlightCosMax = SpotFocus.Lerp(light.SpotlightCosMin, 1f);
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _shadows);
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = ParentMatrix.Invert_v2();
            Direction = Vector3.TransformNormal(Vector3.TransformNormal(ActualDirection, Matrix.RotationQuaternion(delta)), parentMatrixInvert);
        }

        protected override IRenderableObject CreateDummy() {
            return DebugLinesObject.GetLinesCone(Angle, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f));
        }

        protected override Matrix GetDummyTransformMatrix(ICamera camera) {
            return ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY).ToFixedSizeMatrix(camera);
        }
    }
}