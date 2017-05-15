using System;
using System.Collections.Generic;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(newType), newType, null);
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
            get { return _direction; }
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
            get { return _angle; }
            set {
                if (value.Equals(_angle)) return;
                _angle = value;
                DisposeHelper.Dispose(ref _dummy);
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private float _spotFocus = 0.5f;

        public float SpotFocus {
            get { return _spotFocus; }
            set {
                if (value.Equals(_spotFocus)) return;
                _spotFocus = value;
                OnPropertyChanged();
            }
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
                size = new Vector4(_shadows.MapSize, _shadows.MapSize, 1f / _shadows.MapSize, 1f / _shadows.MapSize);
                matrix = _shadows.ShadowTransform;
                view = _shadows.View;
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.DirectionW = -ActualDirection;
            light.Range = Range;
            light.SpotlightCosMin = Angle.Cos();
            light.SpotlightCosMax = light.SpotlightCosMin.Lerp(1f, SpotFocus);
            light.Type = EffectDarkMaterial.LightSpot;
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

        private RenderableList _dummy;

        private static DebugLinesObject GetLinesCone(float angle, Vector3 direction, Color4 color, int segments = 8, int subSegments = 10, float size = 0.1f) {
            var vertices = new List<InputLayouts.VerticePC> { new InputLayouts.VerticePC(Vector3.Zero, color) };
            var indices = new List<ushort>();

            direction.Normalize();

            Vector3 left, up;
            if (Vector3.Dot(direction, Vector3.UnitY).Abs() > 0.5f) {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            } else {
                left = Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitY));
                up = Vector3.Normalize(Vector3.Cross(direction, left));
            }

            var angleSin = angle.Sin();
            var angleCos = angle.Cos();

            left *= size * angleSin;
            up *= size * angleSin;
            direction *= size * angleCos;

            var total = segments * subSegments;
            for (var i = 1; i <= total; i++) {
                var a = MathF.PI * 2f * i / total;
                vertices.Add(new InputLayouts.VerticePC(left * a.Cos() + up * a.Sin() + direction, color));
                indices.Add((ushort)i);
                indices.Add((ushort)(i == total ? 1 : i + 1));

                if (i % subSegments == 1) {
                    indices.Add(0);
                    indices.Add((ushort)i);
                }
            }

            return new DebugLinesObject(Matrix.Identity, vertices.ToArray(), indices.ToArray());
        }

        public override void DrawDummy(IDeviceContextHolder holder, ICamera camera) {
            if (_dummy == null) {
                _dummy = new RenderableList {
                    GetLinesCone(Angle, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f))
                };
            }

            _dummy.ParentMatrix = ActualPosition.LookAtMatrixXAxis(ActualPosition + ActualDirection, Vector3.UnitY);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}