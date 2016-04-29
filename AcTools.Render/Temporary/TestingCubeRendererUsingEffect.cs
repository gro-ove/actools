using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Temporary {
    public class TestingCubeRendererUsingEffect : SceneRenderer {
        private CameraOrbit CameraOrbit => Camera as CameraOrbit;

        private RenderableList _box1, _box2, _box2s, _box3, _box4;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

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

        protected override void OnTick(float dt) {
            CameraOrbit.Alpha += dt * 0.09f;
            CameraOrbit.Beta = MathF.Sin(Elapsed * 0.05f) * 0.7f;

            _box1.LocalMatrix = Matrix.Scaling(new Vector3(0.8f - 0.7f * MathF.Abs(MathF.Sin(Elapsed * 5.0f)))) * Matrix.Translation(3.2f, 0.0f, 0.0f);
            _box2.LocalMatrix = Matrix.Scaling(new Vector3(0.5f + 0.3f * MathF.Sin(Elapsed))) * Matrix.Translation(-3.2f, 0f, 0f) *  Matrix.RotationY(Elapsed);
            _box3.LocalMatrix = Matrix.Scaling(0.3f, 0.3f, 1.8f)  *
                Matrix.LookAtRH(new Vector3(0f, 2f, 0f), _box2s.Matrix.GetTranslationVector(), Vector3.UnitY);
        }
    }
}
