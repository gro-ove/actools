using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using Newtonsoft.Json.Linq;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkAreaLtcTubeLight : DarkAreaTubeLight {
        public DarkAreaLtcTubeLight() : base(DarkLightType.LtcTube) {}

        protected override IRenderableObject CreateDummy() {
            return DebugLinesObject.GetLinesCylinder(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, Radius, Length);
        }

        private bool _withCaps;

        public bool WithCaps {
            get => _withCaps;
            set {
                if (Equals(value, _withCaps)) return;
                _withCaps = value;
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            if (WithCaps) {
                light.Flags |= EffectDarkMaterial.LightLtcTubeWithCaps;
            }
        }

        protected override VisibleLightObject CreateLightMesh() {
            var s = (Radius.Clamp(0.25f, 0.8f) * 80).RoundToInt();
            var mesh = GeometryGenerator.CreateCylinder(Radius, Radius, Length, s, 1, WithCaps);
            return new VisibleLightObject(DisplayName,
                    mesh.Vertices.Select(x => new InputLayouts.VerticePC(x.Position, default(Vector4))).ToArray(),
                    mesh.Indices.ToArray());
        }
    }

    public class DarkAreaTubeLight : DarkAreaLightBase {
        public DarkAreaTubeLight() : base(DarkLightType.Tube) {}

        protected DarkAreaTubeLight(DarkLightType type) : base(type) {
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
                        Radius = Radius,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.Tube:
                    return new DarkAreaTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Radius,
                        Length = Length,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcPlane:
                    return new DarkAreaPlaneLight {
                        Direction = Direction,
                        Range = Range,
                        Width = Radius,
                        Height = Length,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcTube:
                    return new DarkAreaLtcTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Radius,
                        Length = Length,
                        VisibleLight = VisibleLight
                    };
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        private Vector3 _direction = new Vector3(1f, 0f, 0f);

        public Vector3 Direction {
            get => _direction;
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
            get => _range;
            set {
                if (value.Equals(_range)) return;
                _range = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private float _radius = 0.2f;

        public float Radius {
            get => _radius;
            set {
                if (Equals(value, _radius)) return;
                _radius = value;
                DisposeHelper.Dispose(ref _dummy);
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        private float _length = 1f;

        public float Length {
            get => _length;
            set {
                if (Equals(value, _length)) return;
                _length = value;
                DisposeHelper.Dispose(ref _dummy);
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.PosW = ActualPosition - ActualDirection * Length / 2f;
            light.DirectionW = ActualPosition + ActualDirection * Length / 2f;
            light.Range = Range;
            light.SpotlightCosMin = Radius;
        }

        protected override void DisposeOverride() {
            base.DisposeOverride();
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

        protected override IRenderableObject CreateDummy() {
            return DebugLinesObject.GetLinesRoundedCylinder(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, Radius, Length);
        }

        protected override Matrix GetDummyTransformMatrix(ICamera camera) {
            return ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY);
        }

        protected override Matrix GetLightMeshTransformMatrix() {
            return ActualPosition.LookAtMatrix(ActualPosition + ActualDirection, Vector3.UnitY);
        }

        protected override VisibleLightObject CreateLightMesh() {
            var s = (_radius.Clamp(0.25f, 0.8f) * 80).RoundToInt();
            var mesh = GeometryGenerator.CreateCylinder(_radius, _radius, _length, s, 1, false);
            return new VisibleLightObject(DisplayName,
                    mesh.Vertices.Select(x => new InputLayouts.VerticePC(x.Position, default(Vector4))).ToArray(),
                    mesh.Indices.ToArray());
        }
    }
}