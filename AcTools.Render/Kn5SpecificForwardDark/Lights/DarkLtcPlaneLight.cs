using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Render.Utils;
using AcTools.Utils;
using Newtonsoft.Json.Linq;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkLtcPlaneLight : DarkAreaLightBase {
        public DarkLtcPlaneLight() : base(DarkLightType.LtcPlane) {}

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
                case DarkLightType.AreaSphere:
                    return new DarkAreaSphereLight {
                        Range = Range,
                        Radius = Width,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.AreaTube:
                    return new DarkAreaTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Width,
                        Length = Height,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcPlane:
                    return new DarkLtcPlaneLight {
                        Direction = Direction,
                        Up = Up,
                        Range = Range,
                        Width = Width,
                        Height = Height,
                        VisibleBrightnessMultiplier = VisibleBrightnessMultiplier,
                        DoubleSide = DoubleSide,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcTube:
                    return new DarkLtcTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Width,
                        Length = Height,
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

        private Vector3 _up = new Vector3(0f, 1f, 0f);

        public Vector3 Up {
            get => _up;
            set {
                value = Vector3.Normalize(value);
                if (value.Equals(_up)) return;
                _up = value;
                _actualUp = null;
                OnPropertyChanged();
            }
        }

        private Vector3? _actualUp;
        public Vector3 ActualUp => _actualUp ?? (_actualUp = Vector3.TransformNormal(Up, ParentMatrix)).Value;

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            //obj["range"] = Range;
            obj["width"] = Width;
            obj["height"] = Height;
            obj["meshBrightness"] = VisibleBrightnessMultiplier;
            obj["up"] = VectorToString(Up);
            obj["direction"] = VectorToString(Direction);
            if (DoubleSide) {
                obj["doubleSide"] = DoubleSide;
            }
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitX;
            Up = StringToVector((string)obj["up"]) ?? Vector3.UnitY;
            //Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Width = obj["width"] != null ? (float)obj["width"] : 0.6f;
            Height = obj["height"] != null ? (float)obj["height"] : 0.4f;
            VisibleBrightnessMultiplier = obj["meshBrightness"] != null ? (float)obj["meshBrightness"] : 1f;
            DoubleSide = obj["doubleSide"] != null && (bool)obj["doubleSide"];
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

        private float _width = 0.6f;

        public float Width {
            get => _width;
            set {
                if (Equals(value, _width)) return;
                _width = value;
                ResetDummy();
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        private float _height = 0.4f;

        public float Height {
            get => _height;
            set {
                if (Equals(value, _height)) return;
                _height = value;
                ResetDummy();
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        private bool _doubleSide;

        public bool DoubleSide {
            get => _doubleSide;
            set {
                if (Equals(value, _doubleSide)) return;
                _doubleSide = value;
                ResetDummy();
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);

            var pos = ActualPosition;
            var matrix = Vector3.Zero.LookAtMatrixConsiderUp(ActualDirection, ActualUp);
            var dx = Vector3.TransformNormal(Vector3.UnitZ, matrix) * Width / 2f;
            var dy = Vector3.TransformNormal(Vector3.UnitY, matrix) * Height / 2f;

            // p0, p1
            light.PosW = pos - dx - dy;
            light.DirectionW = pos + dx - dy;

            // p2
            var p2 = pos + dx + dy;
            light.SpotlightCosMin = p2.X;
            light.SpotlightCosMax = p2.Y;
            light.Padding = p2.Z;

            // p3
            light.Extra = new Vector4(pos - dx + dy, 0f);

            if (DoubleSide) {
                light.Flags |= EffectDarkMaterial.LightLtcPlaneDoubleSide;
            }
        }

        protected override void FlipPreviousY(ref EffectDarkMaterial.Light light) {
            base.FlipPreviousY(ref light);
            light.SpotlightCosMax *= -1f;
            light.Extra.Y *= -1f;
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = ParentMatrix.Invert_v2();
            Direction = Vector3.TransformNormal(Vector3.TransformNormal(ActualDirection, Matrix.RotationQuaternion(delta)), parentMatrixInvert);
            Up = Vector3.TransformNormal(Vector3.TransformNormal(ActualUp, Matrix.RotationQuaternion(delta)), parentMatrixInvert);
        }

        protected override MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.All);
        }

        protected override IRenderableObject CreateDummy() {
            var result = new RenderableList {
                DebugLinesObject.GetLinesPlane(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), Width, Height),
                DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f)
            };

            if (DoubleSide) {
                result.Add(DebugLinesObject.GetLinesArrow(Matrix.Identity, -Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f));
            }

            return result;
        }

        protected override Matrix GetDummyTransformMatrix(ICamera camera) {
            return GetLightMeshTransformMatrix();
        }

        protected override VisibleLightObject CreateLightMesh() {
            using (var p = DebugLinesObject.GetLinesPlane(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), Width, Height)) {
                return new VisibleLightObject(DisplayName, p.Vertices, DoubleSide ?
                        new ushort[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 } :
                        new ushort[] { 0, 1, 2, 0, 2, 3 });
            }

        }

        private float _visibleBrightnessMultiplier = 1f;

        public float VisibleBrightnessMultiplier {
            get => _visibleBrightnessMultiplier;
            set {
                if (Equals(value, _visibleBrightnessMultiplier)) return;
                _visibleBrightnessMultiplier = value;
                OnPropertyChanged();
            }
        }

        protected override Vector4 LightMeshColor => Color.ToVector4() * ((Brightness / 2f).Pow(0.4f) * 4f) * VisibleBrightnessMultiplier;

        protected override Matrix GetLightMeshTransformMatrix() {
            return ActualPosition.LookAtMatrixConsiderUp(ActualPosition + ActualDirection, ActualUp);
        }
    }
}