using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public class InterpolationCamera : ICamera {
        public float Smoothiness { get; }

        public InterpolationCamera(float smoothiness) {
            Smoothiness = smoothiness;
        }

        public Vector3 Position { get; set; }

        public Matrix ViewProj { get; set; }

        public Matrix Proj { get; set; }

        public Matrix View { get; set; }

        public Matrix ViewProjInvert { get; set; }

        public bool Visible(BoundingBox box) {
            return _camera?.Visible(box) == true;
        }

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            return _camera?.Intersect(box) ?? FrustrumIntersectionType.None;
        }

        bool _first = true;
        CameraBase _camera;

        public void Update(CameraBase camera, float dt) {
            _camera = camera;

            if (_first) {
                Position = camera.Position;
                ViewProj = camera.ViewProj;
                Proj = camera.Proj;
                View = camera.Proj;
                ViewProjInvert = camera.ViewProjInvert;
                _first = false;
            } else {
                Position = (Position * Smoothiness + camera.Position) / (1f + Smoothiness);
                ViewProj = (ViewProj * Smoothiness + camera.ViewProj) / (1f + Smoothiness);
                Proj = (Proj * Smoothiness + camera.Proj) / (1f + Smoothiness);
                View = (View * Smoothiness + camera.View) / (1f + Smoothiness);
                ViewProjInvert = (ViewProjInvert * Smoothiness + camera.ViewProjInvert) / (1f + Smoothiness);
            }

            FarZValue = camera.FarZValue;
            NearZValue = camera.NearZValue;
            Up = camera.Up;
            Look = camera.Look;
            Right = camera.Right;
        }

        public Ray GetPickingRay(Vector2 from, Vector2 screenDims) {
            throw new System.NotImplementedException();
        }

        public float FarZValue { get; private set; }
        public float NearZValue { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 Look { get; private set; }
    }
}