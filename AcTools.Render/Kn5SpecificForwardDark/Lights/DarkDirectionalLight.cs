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
    public class DarkDirectionalLight : DarkLightBase {
        public DarkDirectionalLight() : base(DarkLightType.Directional) { }

        protected override DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            switch (newType) {
                case DarkLightType.Point:
                    return new DarkPointLight();
                case DarkLightType.Directional:
                    return new DarkDirectionalLight {
                        _shadowsSize = _shadowsSize,
                        IsMainLightSource = IsMainLightSource,
                        Direction = Direction
                    };
                case DarkLightType.Spot:
                    return new DarkSpotLight {
                        Direction = Direction
                    };
                case DarkLightType.Plane:
                    return new DarkPlaneLight {
                        Direction = Direction
                    };
                case DarkLightType.AreaSphere:
                    return new DarkAreaSphereLight();
                case DarkLightType.AreaTube:
                    return new DarkAreaTubeLight();
                case DarkLightType.LtcPlane:
                    return new DarkLtcPlaneLight {
                        Direction = Direction
                    };
                case DarkLightType.LtcTube:
                    return new DarkLtcTubeLight();
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["direction"] = VectorToString(Direction);
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Direction = StringToVector((string)obj["direction"]) ?? Vector3.UnitY;
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

        private float _shadowsSize = 50f;
        private bool _shadowsSizeChanged;

        public float ShadowsSize {
            get => _shadowsSize;
            set {
                if (Equals(value, _shadowsSize)) return;
                _shadowsSize = value;
                _shadowsSizeChanged = true;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private ShadowsDirectional _shadows;
        private bool _shadowsCleared;

        private bool _isMainLightSource;

        public bool IsMainLightSource {
            get => _isMainLightSource;
            set {
                if (Equals(value, _isMainLightSource)) return;
                _isMainLightSource = value;
                OnPropertyChanged();
            }
        }

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (IsMainLightSource) return;

            if (_shadows == null) {
                _shadows = new ShadowsDirectional(ShadowsResolution, new[] { ShadowsSize });
                _shadowsSizeChanged = false;
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);

                if (_shadowsSizeChanged) {
                    _shadowsSizeChanged = false;
                    _shadows.SetSplits(holder, new [] { ShadowsSize });
                }

                if (_shadows.Update(ActualDirection, shadowsPosition)) {
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
                matrix = _shadows.Splits[0].ShadowTransform;
                view = _shadows.Splits[0].View;
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.DirectionW = -ActualDirection;
            light.DirectionW.Normalize();
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _shadows);
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = Matrix.Invert(ParentMatrix);
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