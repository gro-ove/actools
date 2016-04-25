using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.DeferredShading;
using AcTools.Render.DeferredShading.Lights;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5Specific {
    public class Kn5ObjectRenderer : SpriteDeferredShadingRenderer {
        private readonly Kn5[] _kn5;
        private readonly string _mainDirectory;
        private readonly DataWrapper _mainData;

        public bool AutoRotate = true;

        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        protected override Vector3 GetReflectionCubemapPosition() {
            return CameraOrbit.Target;
        }

        public Kn5ObjectRenderer(string mainKn5Filename, params string[] additionalKn5Filenames) {
            _kn5 = new[] { mainKn5Filename }.Union(additionalKn5Filenames).Where(x => x != null).Select(Kn5.FromFile).ToArray();
            _mainDirectory = Path.GetDirectoryName(mainKn5Filename);
            _mainData = DataWrapper.FromFile(_mainDirectory);

            AmbientLower = Vector3.Normalize(new Vector3(114f, 124f, 147f));
            AmbientUpper = Vector3.Normalize(new Vector3(85f, 105f, 128f));
        }

        private Kn5RenderableList FindParentNodeByName(string name) {
            return Scene.OfType<RenderableList>().SelectManyRecursive(x => x.OfType<Kn5RenderableList>())
                        .OfType<Kn5RenderableList>().FirstOrDefault(x => x.OriginalNode.Name == name);
        }

        private void LoadBodyAmbientShadow() {
            var iniFile = _mainData.GetIniFile("ambient_shadows.ini");
            var ambientBodyShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));

            var filename = Path.Combine(_mainDirectory, "body_shadow.png");
            Scene.Add(new AmbientShadow(filename, Matrix.Scaling(ambientBodyShadowSize) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(0f, 0.01f, 0f)));
        }

        private void LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = FindParentNodeByName(nodeName);
            if (node == null) return;

            var wheel = FindParentNodeByName(nodeName).Matrix.GetTranslationVector();
            wheel.Y = 0.001f;

            var filename = Path.Combine(_mainDirectory, textureName);
            Scene.Add(new AmbientShadow(filename, Matrix.Scaling(0.3f, 1.0f, 0.3f) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(wheel)));
        }

        private void LoadAmbientShadows() {
            if (_mainData.IsEmpty) return;

            LoadBodyAmbientShadow();
            LoadWheelAmbientShadow("WHEEL_LF", "tyre_0_shadow.png");
            LoadWheelAmbientShadow("WHEEL_RF", "tyre_1_shadow.png");
            LoadWheelAmbientShadow("WHEEL_LR", "tyre_2_shadow.png");
            LoadWheelAmbientShadow("WHEEL_RR", "tyre_3_shadow.png");
        }

        private IEnumerable<Kn5RenderableObject> GetByNameInner(string name, RenderableList parent) {
            foreach (var obj in parent) {
                var r = obj as Kn5RenderableObject;
                if (r?.OriginalNode.Name == name) {
                    yield return r;
                }

                var l = obj as RenderableList;
                if (l == null) continue;
                foreach (var o in GetByNameInner(name, l)) {
                    yield return o;
                }
            }
        }

        protected IEnumerable<Kn5RenderableObject> GetByNameAll(string name, IRenderableObject parent = null) {
            var p = (parent ?? Scene) as RenderableList;
            return p != null ? GetByNameInner(name, p) : new Kn5RenderableObject[0];
        }

        protected Kn5RenderableObject GetByName(string name, IRenderableObject parent = null) {
            return GetByNameAll(name, parent).FirstOrDefault();
        }

        protected override void InitializeInner() {
            base.InitializeInner();
            Scene.Add(SkyObject.Create(500f));

            var mainNode = true;
            foreach (var kn5 in _kn5) {
                Kn5MaterialsProvider.Initialize(kn5);
                TexturesProvider.Initialize(kn5);

                var node = Kn5Converter.Convert(kn5.RootNode);
                if (mainNode) {
                    var list = node as RenderableList;
                    if (list != null) {
                        list.LocalMatrix = Matrix.Translation(0, -list.WorldBoundingBox?.Minimum.Y ?? 0f, 0) * list.LocalMatrix;
                    }

                    var iniFile = _mainData.GetIniFile("mirrors.ini");
                    var mirrors = iniFile.GetSections("MIRROR").Select(x => x["NAME"]).ToArray();

                    foreach (var obj in mirrors.Select(mirror => GetByName(mirror, node)).Where(obj => obj != null)) {
                        obj.SwitchToMirror();
                    }

                    node.IsReflectable = false;
                    mainNode = false;
                }

                Scene.Add(node);
            }

            LoadAmbientShadows();

            var steer = FindParentNodeByName("STEER_HR");
            Camera = new CameraOrbit(32) {
                Alpha = 30.0f,
                Beta = 25.0f,
                NearZ = 0.2f,
                FarZ = 500f,
                Radius = 4.8f,
                Target = steer == null ? new Vector3(0f, 0.5f, 0f) : steer.Matrix.GetTranslationVector() - Vector3.UnitY * 0.25f
            };

            //Lights.Add(new PointLight {
            //    Radius = 200f,
            //    Color = FixLight(new Vector3(1.2f, 1.0f, 0.9f)),
            //    Position = new Vector3(1.2f, 3.4f, 2.2f)
            //});

            Sun = new DirectionalLight {
                Color = FixLight(new Vector3(1.2f, 1.0f, 0.9f)) * 5f,
                Direction = Vector3.Normalize(new Vector3(-1.2f, -3.4f, -2.2f))
            };

            foreach (var i in Enumerable.Range(0, 0)) {
                AddLight();
            }
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
                Radius = 0.01f * _random.Next(500, 1500),
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

        public bool Moving = true;

        protected override void Update(float dt) {
            if (!Moving) return;

            if (AutoRotate) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta = MathF.Sin(Elapsed * 0.09f) * 0.4f;
            }

            Sun.Direction = new Vector3(
                        MathF.Sin(Elapsed * 0.15f), -1.5f,
                        MathF.Sin(Elapsed * 0.23f + 0.07f));

            foreach (var i in _pointLights) {
                i.Light.Position = new Vector3(
                        MathF.Sin(Elapsed * i.B + i.D) * i.C, 1.5f + i.A,
                        MathF.Sin(Elapsed * i.E + i.F) * i.G);
            }
        }

        public override void Dispose() {
            base.Dispose();
            foreach (var kn5 in _kn5) {
                Kn5MaterialsProvider.DisposeFor(kn5);
                TexturesProvider.DisposeFor(kn5);
            }
        }
    }
}
