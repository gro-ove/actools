using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Deferred.Kn5Specific.Materials;
using AcTools.Render.Deferred.Lights;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Deferred.Kn5Specific {
    public class Kn5ObjectRenderer : StatsDeferredShadingRenderer, IKn5ObjectRenderer {
        private readonly Kn5[] _kn5;

        public FpsCamera FpsCamera => null;

        public bool AutoRotate { get; set; } = true;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool UseFpsCamera {
            get { return false; }
            set { }
        }

        bool IKn5ObjectRenderer.VisibleUi {
            get { return VisibleUi; }
            set { VisibleUi = value; }
        }

        private DirectionalLight _sun;
        public bool Daylight {
            get { return Sun != null; }
            set {
                if (Equals(value, Daylight)) return;
                if (value) {
                    Sun = _sun;
                } else {
                    _sun = Sun;
                    Sun = null;
                }
            }
        }

        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        protected override Vector3 ReflectionCubemapPosition => CameraOrbit?.Target ?? Vector3.Zero;

        public Kn5ObjectRenderer(string mainKn5Filename, params string[] additionalKn5Filenames) {
            _kn5 = new[] { mainKn5Filename }.Union(additionalKn5Filenames).Where(x => x != null).Select(x => Kn5.FromFile(x)).ToArray();

            AmbientLower = Vector3.Normalize(new Vector3(114f, 124f, 147f));
            AmbientUpper = Vector3.Normalize(new Vector3(85f, 105f, 128f));
        }

        private EffectDeferredGObject _effect;

        protected override void DrawPrepare() {
            base.DrawPrepare();

            _effect.FxAmbientDown.Set(AmbientLower);
            _effect.FxAmbientRange.Set(AmbientUpper - AmbientLower);

            if (Sun != null) {
                _effect.FxDirectionalLightDirection.Set(Sun.Direction);
                _effect.FxLightColor.Set(AmbientLight(Sun.Color));
            } else {
                _effect.FxDirectionalLightDirection.Set(Vector3.Zero);
                _effect.FxLightColor.Set(Vector3.Zero);
            }
        }

        public class DeferredCarLight : CarLight, ILight {
            [CanBeNull]
            private PointLight[] _lights;

            protected override void SetEmissive(Vector3 value) {
                base.SetEmissive(value);

                _lights?.DisposeEverything();
                _lights = value == default(Vector3) ? null : CreateLights(value);
            }

            public void Draw(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode) {
                if (_lights == null) return;
                foreach (var light in _lights) {
                    light.Draw(holder, camera, mode);
                }
            }

            private class InnerPair {
                public Vector3 Position;
                public Vector3 Normal;
                public int Count;
            }

            private PointLight[] CreateLights(Vector3 emissive) {
                if (Node?.BoundingBox.HasValue != true) return null;

                var inv = Matrix.Invert(Matrix.Transpose(Node.ParentMatrix));
                var node = Node as TrianglesRenderableObject<InputLayouts.VerticePNTG>;
                if (node == null) return null;

                var vertices = node.Vertices.Select(x => new {
                    Position = Vector3.TransformCoordinate(x.Position, Node.ParentMatrix),
                    Normal = Vector3.TransformCoordinate(x.Normal, inv)
                }).ToArray();
                var list = new List<InnerPair>();

                var threshold = 0.25f;
                foreach (var v in vertices) {
                    var min = float.MaxValue;
                    InnerPair minPair = null;
                    foreach (var t in list) {
                        var p = t;
                        var d = (v.Position - t.Position).LengthSquared();
                        if (d > min || d > threshold) continue;
                        min = d;
                        minPair = p;
                    }

                    if (minPair == null) {
                        list.Add(new InnerPair { Position = v.Position, Normal = v.Normal, Count = 1 });
                    } else {
                        minPair.Position = (minPair.Position * minPair.Count + v.Position) / (1f + minPair.Count);
                        minPair.Normal = (minPair.Normal * minPair.Count + v.Normal) / (1f + minPair.Count);
                        minPair.Count++;
                    }
                }

                for (var i = 1; i < list.Count; i++) {
                    var v = list[i];
                    for (var j = 0; j < i; j++) {
                        var t = list[j];
                        var d = (v.Position - t.Position).LengthSquared();
                        if (d <= threshold) {
                            v.Position = (v.Position * v.Count + t.Position * t.Count) / (v.Count + t.Count);
                            v.Normal = (v.Normal * v.Count + t.Normal * t.Count) / (v.Count + t.Count);
                            v.Count += t.Count;
                            list.Remove(v);
                            list.Remove(t);

                            i = 0;
                            break;
                        }
                    }
                }

                var headlight = emissive.X / 4 < emissive.Y;
                var limit = headlight ? 90f : 120f;
                if (emissive.GetBrightness() > limit) {
                    emissive *= limit / emissive.GetBrightness();
                }

                return list.Select(x => new PointLight {
                    Position = x.Position + x.Normal * (0.15f + emissive.GetBrightness() * (headlight ? 0.007f : 0.003f)),
                    Radius = 1.6f,
                    Specular = false,
                    Color = emissive / 500f
                }).ToArray();
            }

            public void Dispose() {
                _lights?.DisposeEverything();
                _lights = null;
            }
        }

        private class DeferredRenderableCar : Kn5RenderableCar {
            private readonly List<ILight> _lights;

            public DeferredRenderableCar(CarDescription car, Matrix matrix, string selectSkin = null, bool scanForSkins = true,
                    float shadowsHeight = 0, List<ILight> lights = null)
                    : base(car, matrix, selectSkin ?? DefaultSkin, scanForSkins, shadowsHeight) {
                _lights = lights;
            }

            protected override IEnumerable<CarLight> LoadLights() {
                var result = LoadLights<DeferredCarLight>().ToList();
                _lights?.AddRange(result);
                return result;
            }
        }

        public bool CarLightsEnabled {
            get { return _car?.LightsEnabled == true; }
            set {
                if (_car != null) {
                    _car.LightsEnabled = value;
                }
            }
        }

        public bool CarBrakeLightsEnabled {
            get { return _car?.BrakeLightsEnabled == true; }
            set {
                if (_car != null) {
                    _car.BrakeLightsEnabled = value;
                }
            }
        }

        public void SelectPreviousSkin() {
            _car?.SelectPreviousSkin(DeviceContextHolder);
        }

        public void SelectNextSkin() {
            _car?.SelectNextSkin(DeviceContextHolder);
        }

        public void SelectSkin(string skinId) {
            _car?.SelectSkin(DeviceContextHolder, skinId);
        }

        private Kn5RenderableCar _car;

        protected override void InitializeInner() {
            base.InitializeInner();
            DeviceContextHolder.Set<IMaterialsFactory>(new MaterialsProviderDeferred());
            
            Scene.Add(SkyObject.Create(500f));

            foreach (var showroom in _kn5.Skip(1)) {
                Scene.Add(new Kn5RenderableFile(showroom, Matrix.Identity));
            }

            if (_kn5.Length > 1) {
                _car = new DeferredRenderableCar(CarDescription.FromKn5(_kn5[0]), Matrix.Identity, shadowsHeight: 0.001f, lights: Lights) {
                    IsReflectable = false
                };
                Scene.Add(_car);
            }

            Scene.UpdateBoundingBox();

            Camera = new CameraOrbit(32) {
                Alpha = 0.9f,
                Beta = 0.1f,
                Radius = _car?.BoundingBox?.GetSize().Length() * 1.2f ?? 4.8f,
                Target = (_car?.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.05f, 0f)
            };

            _resetCamera = (CameraOrbit)Camera.Clone();

            Sun = new DirectionalLight {
                Color = FixLight(new Vector3(1.2f, 1.0f, 0.9f)) * 5f,
                Direction = Vector3.Normalize(new Vector3(-1.2f, -3.4f, -2.2f))
            };

            _effect = DeviceContextHolder.GetEffect<EffectDeferredGObject>();
        }

        private float _resetState;
        private CameraOrbit _resetCamera;

        public void ResetCamera() {
            AutoRotate = true;
            _resetState = 1f;
        }

        public void ChangeCameraFov(float newFovY) {
            var c = CameraOrbit;
            if (c == null) return;

            var delta = newFovY - c.FovY;
            c.FovY = newFovY.Clamp(MathF.PI * 0.01f, MathF.PI * 0.8f);
            c.SetLens(c.Aspect);
            c.Zoom(-delta * 4f);
        }

        private class PointLightDesc {
            public PointLight Light;
            public float A, B, C, D, E, F, G;
        }

        private readonly List<PointLightDesc> _pointLights = new List<PointLightDesc>();
        private readonly Random _random = new Random();

        public void AddLight() {
            var color = new Vector3((float)_random.NextDouble(), (float)_random.NextDouble(),
                    (float)_random.NextDouble());
            color.Normalize();
            var light = new PointLight {
                // Radius = 0.01f * _random.Next(100, 500),
                Radius = 0.004f * _random.Next(100, 500),
                Color = FixLight(color)
            };

            Lights.Add(light);
            _pointLights.Add(new PointLightDesc {
                Light = light,
                A = (float)_random.NextDouble(),
                B = (float)_random.NextDouble(),
                C = 5.0f + (float)_random.NextDouble(),
                D = (float)_random.NextDouble(),
                E = (float)_random.NextDouble(),
                F = (float)_random.NextDouble(),
                G = 3.0f + (float)_random.NextDouble()
            });
        }

        public void RemoveLight() {
            var first = _pointLights.FirstOrDefault();
            if (first == null) return;

            Lights.Remove(first.Light);
            _pointLights.Remove(first);
        }

        public bool AutoRotateSun = true;
        private float _elapsedCamera, _elapsedSun;

        protected override void OnTick(float dt) {
            const float threshold = 0.001f;
            if (_resetState > threshold) {
                if (!AutoRotate) {
                    _resetState = 0f;
                    return;
                }

                _resetState += (-0f - _resetState) / 10f;
                if (_resetState <= threshold) {
                    AutoRotate = false;
                }

                var cam = CameraOrbit;
                if (cam != null) {
                    cam.Alpha += (_resetCamera.Alpha - cam.Alpha) / 10f;
                    cam.Beta += (_resetCamera.Beta - cam.Beta) / 10f;
                    cam.Radius += (_resetCamera.Radius - cam.Radius) / 10f;
                    cam.FovY += (_resetCamera.FovY - cam.FovY) / 10f;
                    // cam.Target += (_resetCamera.Target - cam.Target) / 10f;
                }

                _elapsedCamera = 0f;

                IsDirty = true;
            } else if (AutoRotate && CameraOrbit != null) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta += (MathF.Sin(_elapsedCamera * 0.39f) * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = _resetCamera.Target + new Vector3(-0.05f * CameraOrbit.Position.X, -0.02f * CameraOrbit.Position.Y, 0f);
                CameraOrbit.Target += (t - CameraOrbit.Target) / 2f;
            }

            if (AutoRotateSun && Sun != null) {
                var dir = Sun.Direction;
                dir.X += (MathF.Sin(_elapsedSun * 0.15f) - dir.X) / 10f;
                dir.Y += (-1.5f - dir.Y) / 10f;
                dir.Z += (MathF.Sin(_elapsedSun * 0.23f + 0.07f) - dir.Z) / 10f;

                Sun.Direction = dir;
                _elapsedSun += dt;
            }

            foreach (var i in _pointLights) {
                i.Light.Position = new Vector3(
                        MathF.Sin(Elapsed * i.B + i.D) * i.C, i.A,
                        MathF.Sin(Elapsed * i.E + i.F) * i.G) * 0.3f;
            }
        }
    }
}
