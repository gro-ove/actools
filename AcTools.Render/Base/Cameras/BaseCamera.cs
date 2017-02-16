using System;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public abstract class BaseCamera : ICamera {
        public Vector3 Position { get; set; }

        public Vector3 Right;
        public Vector3 Up;
        public Vector3 Look;
        public float? NearZ;
        public float? FarZ;
        public float Aspect;
        public float FovY;

        private float _nearZFovY, _nearZCached;

        public float NearZValue {
            get {
                if (NearZ.HasValue) return NearZ.Value;
                if (Equals(_nearZFovY, FovY)) return _nearZCached;
                _nearZFovY = FovY;
                return _nearZCached = 0.04f / MathF.Pow(_nearZFovY, 2f);
            }
        }

        private float _farZFovY, _farZCached;

        public float FarZValue {
            get {
                if (FarZ.HasValue) return FarZ.Value;
                if (Equals(_farZFovY, FovY)) return _farZCached;
                _farZFovY = FovY;
                return _farZCached = 1e3f / MathF.Pow(_farZFovY, 0.5f);
            }
        }

        public Matrix View { get; private set; }

        protected void SetView(Matrix view) {
            if (view.Equals(View)) return;
            View = view;
            _viewProj = null;
            _viewInvert = null;
            _viewProjInvert = null;
        }

        public Matrix Proj { get; private set; }

        protected void SetProj(Matrix proj) {
            if (proj.Equals(Proj)) return;
            Proj = proj;
            _viewProj = null;
            _viewProjInvert = null;
        }
        
        public Matrix ViewProj => _viewProj ?? (_viewProj = View * Proj).Value;
        private Matrix? _viewProj;

        public Matrix ViewInvert => _viewInvert ?? (_viewInvert = Matrix.Invert(View)).Value;
        private Matrix? _viewInvert;

        public Matrix ViewProjInvert => _viewProjInvert ?? (_viewProjInvert = Matrix.Invert(ViewProj)).Value;
        private Matrix? _viewProjInvert;

        protected BaseCamera(float fov) {
            Position = new Vector3();
            Right = new Vector3(1, 0, 0);
            Up = new Vector3(0, 1, 0);
            Look = new Vector3(0, 0, 1);
            FovY = fov;

            // NearZ = 0.01f;
            //FarZ = 500.0f;

            SetView(Matrix.Identity);
            SetProj(Matrix.Identity);
        }

        public abstract void LookAt(Vector3 pos, Vector3 target, Vector3 up);

        public abstract void Strafe(float d);

        public abstract void Walk(float d);

        public abstract void Pitch(float angle);

        public abstract void Yaw(float angle);

        public abstract void Zoom(float dr);

        public abstract void UpdateViewMatrix();

        public abstract void Save();

        public abstract void Restore();

        public abstract BaseCamera Clone();

        private void UpdateNearZ() {
            
        }

        public virtual void SetLens(float aspect) {
            Aspect = aspect;
            SetProj(Matrix.PerspectiveFovLH(FovY, Aspect, NearZValue, FarZValue));
        }
        
        public Ray GetPickingRay(Vector2 sp, Vector2 screenDims) {
            // convert screen pixel to view space
            var vx = (2.0f * sp.X / screenDims.X - 1.0f) / Proj.M11;
            var vy = (-2.0f * sp.Y / screenDims.Y + 1.0f) / Proj.M22;

            var ray = new Ray(new Vector3(), new Vector3(vx, vy, 1.0f));
            ray = new Ray(Vector3.TransformCoordinate(ray.Position, ViewInvert), Vector3.TransformNormal(ray.Direction, ViewInvert));
            ray.Direction.Normalize();
            return ray;
        }

        protected Frustum Frustum;

        public bool DisableFrustum { get; set; }

        public virtual bool Visible(BoundingBox box) {
            if (DisableFrustum) return true;
            if (Frustum == null) throw new Exception("Call SetLens() first");
            return Frustum.Intersect(box) > 0;
        }

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            return Frustum.Intersect(box);
        }
    }
}