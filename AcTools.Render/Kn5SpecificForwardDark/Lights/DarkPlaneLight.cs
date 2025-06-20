using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Render.Utils;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkPlaneLight : DarkLightBase {
        public DarkPlaneLight() : base(DarkLightType.Plane) {
            ShadowsAvailable = false;
            HighQualityShadowsAvailable = false;
        }

        protected override DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            switch (newType) {
                case DarkLightType.Point:
                    return new DarkPointLight {
                        Range = Range,
                    };
                case DarkLightType.Directional:
                    return new DarkDirectionalLight {
                        Direction = Direction
                    };
                case DarkLightType.Spot:
                    return new DarkSpotLight {
                        Direction = Direction,
                        Range = Range,
                    };
                case DarkLightType.Plane:
                    return new DarkPlaneLight {
                        Direction = Direction,
                        Range = Range
                    };
                case DarkLightType.AreaSphere:
                    return new DarkAreaSphereLight();
                case DarkLightType.AreaTube:
                    return new DarkAreaTubeLight();
                case DarkLightType.LtcPlane:
                    return new DarkLtcPlaneLight {
                        Direction = Direction,
                        Range = Range
                    };
                case DarkLightType.LtcTube:
                    return new DarkLtcTubeLight();
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["range"] = Range;
            obj["direction"] = VectorToString(Direction);
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitY;
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

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {}

        protected override void SetShadowOverride(out Vector4 size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            size = default(Vector4);
            matrix = Matrix.Identity;
            view = null;
        }

        public override void InvalidateShadows() {}

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.DirectionW = -ActualDirection;
            light.DirectionW.Normalize();
            light.Range = Range;
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = ParentMatrix.Invert_v2();
            Direction = Vector3.TransformNormal(Vector3.TransformNormal(ActualDirection, Matrix.RotationQuaternion(delta)), parentMatrixInvert);
        }

        protected override IRenderableObject CreateDummy() {
            return new RenderableList {
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitY, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitZ, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f)
            };
        }

        protected override Matrix GetDummyTransformMatrix(ICamera camera) {
            return ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY).ToFixedSizeMatrix(camera);
        }
    }
}