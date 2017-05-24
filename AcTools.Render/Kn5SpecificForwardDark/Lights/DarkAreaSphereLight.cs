using System;
using System.Linq;
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
    public class DarkAreaSphereLight : DarkAreaLightBase {
        public DarkAreaSphereLight() : base(DarkLightType.Sphere) {}

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
                        Range = Range,
                        Radius = Radius,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcPlane:
                    return new DarkAreaPlaneLight {
                        Range = Range,
                        Width = Radius,
                        Height = Radius,
                        DoubleSide = true,
                        VisibleLight = VisibleLight
                    };
                case DarkLightType.LtcTube:
                    return new DarkAreaLtcTubeLight {
                        Range = Range,
                        Radius = Radius,
                        VisibleLight = VisibleLight
                    };
                default:
                    return base.ChangeTypeOverride(newType);
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            obj["range"] = Range;
            obj["radius"] = Radius;
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            Range = obj["range"] != null ? (float)obj["range"] : 2f;
            Radius = obj["radius"] != null ? (float)obj["radius"] : 0.2f;
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
                ResetDummy();
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        protected override void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            base.SetOverride(holder, ref light);
            light.Range = Range;
            light.SpotlightCosMin = Radius;
            light.Type = EffectDarkMaterial.LightSphere;
        }

        public override void Rotate(Quaternion delta) {}

        protected override MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.None);
        }

        protected override IRenderableObject CreateDummy() {
            return new RenderableList {
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitY, new Color4(1f, 1f, 1f, 0f), 20, Radius),
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, Radius),
                DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitZ, new Color4(1f, 1f, 1f, 0f), 20, Radius)
            };
        }

        protected override Matrix GetDummyTransformMatrix(ICamera camera) {
            return Matrix.Translation(ActualPosition);
        }

        protected override VisibleLightObject CreateLightMesh() {
            var s = (_radius.Clamp(0.25f, 0.8f) * 100).RoundToInt();
            var mesh = GeometryGenerator.CreateSphere(_radius, s, s);
            return new VisibleLightObject(DisplayName,
                    mesh.Vertices.Select(x => new InputLayouts.VerticePC(x.Position, default(Vector4))).ToArray(),
                    mesh.Indices.ToArray());
        }

        protected override Matrix GetLightMeshTransformMatrix() {
            return Matrix.Translation(ActualPosition);
        }
    }
}