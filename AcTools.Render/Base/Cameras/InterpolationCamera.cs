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
        BaseCamera _camera;

        public void Update(BaseCamera camera, float dt) {
            _camera = camera;

            if (_first) {
                Position = camera.Position;
                ViewProj = camera.ViewProj;
                Proj = camera.Proj;
                View = camera.Proj;
                ViewProjInvert = camera.ViewProjInvert;
                FarZValue = camera.FarZValue;
                NearZValue = camera.NearZValue;
                _first = false;
            } else {
                Position = (Position * Smoothiness + camera.Position) / (1f + Smoothiness);
                ViewProj = (ViewProj * Smoothiness + camera.ViewProj) / (1f + Smoothiness);
                Proj = (Proj * Smoothiness + camera.Proj) / (1f + Smoothiness);
                View = (View * Smoothiness + camera.View) / (1f + Smoothiness);
                ViewProjInvert = (ViewProjInvert * Smoothiness + camera.ViewProjInvert) / (1f + Smoothiness);
            }
        }

        public float FarZValue { get; private set; }

        public float NearZValue { get; private set; }

        public Ray GetPickingRay(Vector2 from, Vector2 screenDims) {
            throw new System.NotImplementedException();
        }
    }
}