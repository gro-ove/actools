using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.DeferredShading;
using AcTools.Render.DeferredShading.Lights;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificDeferred.Materials;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5SpecificDeferred {
    public class Kn5ObjectRenderer : StatsDeferredShadingRenderer, IKn5ObjectRenderer {
        private readonly Kn5[] _kn5;
        private CarHelper _carHelper;

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
            _carHelper = new CarHelper(_kn5.FirstOrDefault());

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

        public List<DeferredCarLight> CarLights;

        public class DeferredCarLight : CarLight, ILight {
            private PointLight[] _lights;

            protected override void OnEnabledChanged(bool value) {
                base.OnEnabledChanged(value);

                if (value && _lights == null) {
                    _lights = CreateLights();
                }
            }

            public void Draw(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode) {
                if (!IsEnabled || _lights == null) return;
                foreach (var light in _lights) {
                    light.Draw(holder, camera, mode);
                }
            }

            private class InnerPair {
                public Vector3 Position;
                public Vector3 Normal;
                public int Count;
            }

            private PointLight[] CreateLights() {
                if (Node?.BoundingBox.HasValue != true) return null;

                var inv = Matrix.Invert(Matrix.Transpose(Node.ParentMatrix));
                var vertices = Node.Vertices.Select(x => new {
                    Position = Vector3.Transform(x.Position, Node.ParentMatrix).GetXyz(),
                    Normal = Vector3.Transform(x.Normal, inv).GetXyz()
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

                var emissive = Emissive;
                var limit = Type == CarLightType.Headlight ? 120f : 90f;
                if (emissive.GetBrightness() > limit) {
                    emissive *= limit / emissive.GetBrightness();
                }

                return list.Select(x => new PointLight {
                    Position = x.Position + x.Normal * (0.15f + emissive.GetBrightness() * (Type == CarLightType.Headlight ? 0.007f : 0.003f)),
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

        private void LoadLights(Kn5RenderableList node) {
            CarLights = _carHelper.LoadLights<DeferredCarLight>(node).ToList();
            Lights.AddRange(CarLights);
        }

        private bool _carLightsEnabled;

        public bool CarLightsEnabled {
            get { return _carLightsEnabled; }
            set {
                if (Equals(_carLightsEnabled, value)) return;
                _carLightsEnabled = value;

                foreach (var light in CarLights) {
                    light.IsEnabled = value;
                }
            }
        }

        public void SelectPreviousSkin() {
            _carHelper.SelectPreviousSkin(DeviceContextHolder);
        }

        public void SelectNextSkin() {
            _carHelper.SelectNextSkin(DeviceContextHolder);
        }

        public void SelectSkin(string skinId) {
            _carHelper.SelectSkin(skinId, DeviceContextHolder);
        }

        private MaterialsProviderDeferred _materialsProvider;
        private TexturesProvider _texturesProvider;

        protected override void InitializeInner() {
            base.InitializeInner();

            _materialsProvider = new MaterialsProviderDeferred();
            _texturesProvider = new TexturesProvider();
            DeviceContextHolder.Set(_materialsProvider);
            DeviceContextHolder.Set(_texturesProvider);
            
            Scene.Add(SkyObject.Create(500f));

            IRenderableObject mainNode = null;
            foreach (var kn5 in _kn5) {
                if (mainNode == null) {
                    _carHelper.SetKn5(DeviceContextHolder);
                } else {
                    _materialsProvider.SetKn5(kn5);
                    _texturesProvider.SetKn5(kn5.OriginalFilename, kn5);
                }

                var node = Kn5Converter.Convert(kn5.RootNode, DeviceContextHolder);
                Scene.Add(node);

                if (mainNode != null) continue;
                node.IsReflectable = false;
                mainNode = node;
            }

            var asList = mainNode as Kn5RenderableList;
            if (asList != null) {
                Scene.AddRange(_carHelper.LoadAmbientShadows(asList, 0.001f));

                _carHelper.AdjustPosition(asList);
                _carHelper.LoadMirrors(asList, DeviceContextHolder);
                LoadLights(asList);
            }

            Scene.UpdateBoundingBox();

            Camera = new CameraOrbit(32) {
                Alpha = 0.9f,
                Beta = 0.1f,
                NearZ = 0.2f,
                FarZ = 500f,
                Radius = mainNode?.BoundingBox?.GetSize().Length() * 1.2f ?? 4.8f,
                Target = (mainNode?.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.05f, 0f)
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

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref _carHelper);
            DisposeHelper.Dispose(ref _materialsProvider);
            DisposeHelper.Dispose(ref _texturesProvider);
        }
    }
}
