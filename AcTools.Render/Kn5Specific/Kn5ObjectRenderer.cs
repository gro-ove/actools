using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
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
    public class Kn5ObjectRenderer : DeferredShadingRenderer {
        private readonly Kn5[] _kn5;
        private readonly string _mainDirectory;
        private readonly DataWrapper _mainData;

        public bool AutoRotate = true;

        public CameraOrbit CameraOrbit {
            get { return Camera as CameraOrbit; }
        }

        protected override Vector3 GetReflectionCubemapPosition() {
            return new Vector3(0f, 0.5f, 0f);
        }

        public Kn5ObjectRenderer(string mainKn5Filename, params string[] additionalKn5Filenames) {
            _kn5 = new []{ mainKn5Filename }.Union(additionalKn5Filenames).Where(x => x != null).Select(Kn5.FromFile).ToArray();
            _mainDirectory = Path.GetDirectoryName(mainKn5Filename);
            _mainData = DataWrapper.FromFile(_mainDirectory);

            AmbientLower = new Vector3(1.0f, 0.95f, 0.8f);
            AmbientUpper = new Vector3(1.0f, 1.0f, 1.0f);
        }

        private Kn5RenderableList FindParentNodeByName(string name) {
            return Scene.OfType<RenderableList>().SelectManyRecursive(x => x.OfType<Kn5RenderableList>())
                .OfType<Kn5RenderableList>().FirstOrDefault(x => x.OriginalNode.Name == name);
        }

        private void LoadBodyAmbientShadow() {
            var iniFile = _mainData.GetIniFile("ambient_shadows.ini");
            var ambientBodyShadowSize = new Vector3(
                (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d)
            );

            var filename = Path.Combine(_mainDirectory, "body_shadow.png");
            Scene.Add(new AmbientShadow(filename, Matrix.Scaling(ambientBodyShadowSize)*Matrix.RotationY(MathF.PI)*
                                                  Matrix.Translation(0f, 0.001f, 0f)));
        }

        private void LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = FindParentNodeByName(nodeName);
            if (node == null) return;

            var wheel = FindParentNodeByName(nodeName).Matrix.GetTranslationVector();
            wheel.Y = 0.001f;

            var filename = Path.Combine(_mainDirectory, textureName);
            Scene.Add(new AmbientShadow(filename, Matrix.Scaling(0.3f, 1.0f, 0.3f)*Matrix.RotationY(MathF.PI)*
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

        protected override void InitializeInner() {
            base.InitializeInner();

            foreach (var kn5 in _kn5) {
                Kn5MaterialsProvider.Initialize(kn5);
                TexturesProvider.Initialize(kn5);
                Scene.Add(Kn5Converter.Convert(kn5.RootNode));
            }

            LoadAmbientShadows();

            var steer = FindParentNodeByName("STEER_HR");
            Camera = new CameraOrbit(45) {
                Alpha = 30.0f,
                Beta = 25.0f,
                NearZ = 0.2f,
                FarZ = 50f,
                Radius = 3.8f,
                Target = steer == null ? new Vector3(0f, 0.5f, 0f) : steer.Matrix.GetTranslationVector() - Vector3.UnitY * 0.25f
            };

            Lights.Add(new PointLight {
                Radius = 96f,
                Color = new Vector3(1.0f, 1.0f, 0.9f),
                Position = new Vector3(1.2f, 3.4f, 2.2f)
            });

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
                Color = color
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

        protected override void Update(float dt) {
            if (AutoRotate) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta = MathF.Sin(Elapsed * 0.09f) * 0.4f;
            }

            foreach (var i in _pointLights) {
                i.Light.Position = new Vector3(
                        MathF.Sin(Elapsed * i.B + i.D) * i.C, 1.5f + i.A,
                        MathF.Sin(Elapsed * i.E + i.F) * i.G);
            }
        }

        public override void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera) {
            foreach (var obj in Scene.Skip(1)) {
                obj.Draw(DeviceContextHolder, camera, SpecialRenderMode.Reflection);
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
