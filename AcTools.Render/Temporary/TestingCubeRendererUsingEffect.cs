using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Temporary {
    public class TestingCube : TrianglesRenderableObject<InputLayouts.VerticePC> {
        private EffectTestingCube _effectMiniCube;

        public TestingCube() : base(new[] {
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f)),

                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f)),

                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),

                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f)),

                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, -1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, 1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(-1.0f, 1.0f, -1.0f), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),

                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, -1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, -1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
                new InputLayouts.VerticePC(new Vector3(1.0f, 1.0f, 1.0f), new Vector4(0.0f, 1.0f, 1.0f, 1.0f)),
            }, Enumerable.Range(0, 36).Select(x => (ushort)x).ToArray()) {}

        protected override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            _effectMiniCube = contextHolder.GetEffect<EffectTestingCube>();
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Default) return;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effectMiniCube.LayoutPC;
            base.DrawInner(contextHolder, camera, mode);

            _effectMiniCube.FxWorldViewProj.SetMatrix(ParentMatrix * camera.ViewProj);
            _effectMiniCube.TechCube.DrawAllPasses(contextHolder.DeviceContext, Indices.Length);
        }

        public override void Dispose() {
            base.Dispose();
            _effectMiniCube.Dispose();
        }
    }

    public class TestingCubeRendererUsingEffect : SceneRenderer {
        private CameraOrbit CameraOrbit {
            get { return Camera as CameraOrbit; }
        }

        private RenderableList _box1, _box2, _box2s, _box3, _box4;

        protected override void InitializeInner() {
            Camera = new CameraOrbit(45) {
                Alpha = 30.0f,
                Beta = 25.0f,
                NearZ = 0.1f,
                Radius = 5.5f,
                Target = Vector3.Zero
            };

            Scene.Add(_box1 = new RenderableList {
                new TestingCube()
            });

            _box2s = new RenderableList(Matrix.RotationX(90.5f) * Matrix.Translation(0.0f, 0.0f, 3.0f)) {new TestingCube()};
            Scene.Add(_box2 = new RenderableList {
                new TestingCube(),
                _box2s
            });
            
            Scene.Add(_box3 = new RenderableList {
                new TestingCube()
            });

            Scene.Add(_box4 = new RenderableList {
                new TestingCube()
            });
        }

        protected override void Update(float dt) {
            CameraOrbit.Alpha += dt * 0.09f;
            CameraOrbit.Beta = MathF.Sin(Elapsed * 0.05f) * 0.7f;

            _box1.LocalMatrix = Matrix.Scaling(new Vector3(0.8f - 0.7f * MathF.Abs(MathF.Sin(Elapsed * 5.0f)))) * Matrix.Translation(3.2f, 0.0f, 0.0f);
            _box2.LocalMatrix = Matrix.Scaling(new Vector3(0.5f + 0.3f * MathF.Sin(Elapsed))) * Matrix.Translation(-3.2f, 0f, 0f) *  Matrix.RotationY(Elapsed);
            _box3.LocalMatrix = Matrix.Scaling(0.3f, 0.3f, 1.8f)  *
                Matrix.LookAtRH(new Vector3(0f, 2f, 0f), _box2s.Matrix.GetTranslationVector(), Vector3.UnitY);
        }
    }
}
