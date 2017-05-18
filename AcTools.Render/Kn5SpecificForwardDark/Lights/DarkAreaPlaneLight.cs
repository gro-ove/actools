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
    public class DarkAreaPlaneLight : DarkLightBase {
        public DarkAreaPlaneLight() : base(DarkLightType.Plane) {
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
                        Radius = Width
                    };
                case DarkLightType.Tube:
                    return new DarkAreaTubeLight {
                        Direction = Direction,
                        Range = Range,
                        Radius = Width,
                        Length = Height,
                    };
                case DarkLightType.Plane:
                    return new DarkAreaPlaneLight {
                        Direction = Direction,
                        Range = Range,
                        Width = Width,
                        Height = Height,
                        DoubleSide = DoubleSide,
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
            obj["width"] = Width;
            obj["height"] = Height;
            obj["direction"] = VectorToString(Direction);
            if (DoubleSide) {
                obj["doubleSide"] = DoubleSide;
            }
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitX;
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Width = obj["width"] != null ? (float)obj["width"] : 0.6f;
            Height = obj["height"] != null ? (float)obj["height"] : 0.4f;
            DoubleSide = obj["doubleSide"] != null && (bool)obj["doubleSide"]; // to avoid smooth enabling
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

        private float _width = 0.6f;

        public float Width {
            get { return _width; }
            set {
                if (Equals(value, _width)) return;
                _width = value;
                DisposeHelper.Dispose(ref _dummy);
                OnPropertyChanged();
            }
        }

        private float _height = 0.4f;

        public float Height {
            get { return _height; }
            set {
                if (Equals(value, _height)) return;
                _height = value;
                DisposeHelper.Dispose(ref _dummy);
                OnPropertyChanged();
            }
        }

        private bool _doubleSide;

        public bool DoubleSide {
            get { return _doubleSide; }
            set {
                if (Equals(value, _doubleSide)) return;
                _doubleSide = value;
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

            var pos = ActualPosition;
            var matrix = Vector3.Zero.LookAtMatrixXAxis(ActualDirection, Vector3.UnitY);
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
                light.Flags |= EffectDarkMaterial.LightPlaneDoubleSide;
            }
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
                    DebugLinesObject.GetLinesPlane(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), Width, Height),
                    DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f)
                };

                if (DoubleSide) {
                    _dummy.Add(DebugLinesObject.GetLinesArrow(Matrix.Identity, -Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f));
                }
            }

            _dummy.ParentMatrix = ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}