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
    public class DarkAreaTubeLight : DarkLightBase {
        public DarkAreaTubeLight() : base(DarkLightType.Tube) {
            ShadowsAvailable = false;
            HighQualityShadowsAvailable = false;
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
                        Range = Range,
                        Radius = Radius
                    };
                case DarkLightType.Tube:
                    return new DarkAreaTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Radius,
                        Length = Length,
                    };
                case DarkLightType.Plane:
                    return new DarkAreaPlaneLight {
                        Direction = Direction,
                        Range = Range,
                        Width = Radius,
                        Height = Length,
                    };
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        private Vector3 _direction = new Vector3(-1f, -1f, -1f);

        public Vector3 Direction {
            get { return _direction; }
            set {
                value = Vector3.Normalize(value);
                if (value.Equals(_direction)) return;
                _direction = value;
                _actualDirection = null;
                OnPropertyChanged();
            }
        }

        private Vector3? _actualDirection;
        public Vector3 ActualDirection => _actualDirection ?? (_actualDirection = Vector3.TransformNormal(Direction, ParentMatrix)).Value;

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["range"] = Range;
            obj["radius"] = Radius;
            obj["length"] = Length;
            obj["direction"] = VectorToString(Direction);
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitX;
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Radius = obj["radius"] != null ? (float)obj["radius"] : 0.2f;
            Length = obj["length"] != null ? (float)obj["length"] : 1f;
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

        private float _radius = 0.2f;

        public float Radius {
            get { return _radius; }
            set {
                if (Equals(value, _radius)) return;
                _radius = value;
                DisposeHelper.Dispose(ref _dummy);
                OnPropertyChanged();
            }
        }

        private float _length = 1f;

        public float Length {
            get { return _length; }
            set {
                if (Equals(value, _length)) return;
                _length = value;
                DisposeHelper.Dispose(ref _dummy);
                OnPropertyChanged();
            }
        }

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {}

        protected override void SetShadowOverride(out Vector4 size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            size = default(Vector4);
            matrix = Matrix.Identity;
            view = null;
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.PosW = ActualPosition - ActualDirection * Length / 2f;
            light.DirectionW = ActualPosition + ActualDirection * Length / 2f;
            light.Range = Range;
            light.SpotlightCosMin = Radius;
            light.Type = (uint)DarkLightType.Tube;
        }

        public override void InvalidateShadows() {}

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _dummy);
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = Matrix.Invert(ParentMatrix);
            Direction = Vector3.TransformNormal(Vector3.TransformNormal(ActualDirection, Matrix.RotationQuaternion(delta)), parentMatrixInvert);
        }

        protected override MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.All);
        }

        private RenderableList _dummy;

        public override void DrawDummy(IDeviceContextHolder holder, ICamera camera) {
            if (_dummy == null) {
                _dummy = new RenderableList {
                    DebugLinesObject.GetLinesRoundedCylinder(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, Radius, Length)
                };
            }

            _dummy.ParentMatrix = ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}