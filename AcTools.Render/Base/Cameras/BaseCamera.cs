using System;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public abstract class BaseCamera : ICamera {
        public Vector3 Position { get; set; }

        public Vector3 Right;
        public Vector3 Up;
        public Vector3 Look;
        public float NearZ;
        public float FarZ;
        public float Aspect;
        public float FovY;

        public float FovX => 2.0f * MathF.Atan(0.5f * NearWindowWidth / NearZ);

        public float NearWindowWidth;
        public float NearWindowHeight;
        public float FarWindowWidth;
        public float FarWindowHeight;
        public Matrix View;
        public Matrix Proj;

        public Matrix ViewProj => View * Proj;

        public Matrix ViewInvert => Matrix.Invert(View);

        public Matrix ViewProjInvert => Matrix.Invert(ViewProj);

        protected BaseCamera(float fov) {
            Position = new Vector3();
            Right = new Vector3(1, 0, 0);
            Up = new Vector3(0, 1, 0);
            Look = new Vector3(0, 0, 1);
            FovY = fov;

            NearZ = 0.01f;
            FarZ = 500.0f;

            View = Matrix.Identity;
            Proj = Matrix.Identity;
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

        public virtual void SetLens(float aspect) {
            Aspect = aspect;

            NearWindowHeight = 2.0f * NearZ * MathF.Tan(0.5f * FovY);
            FarWindowHeight = 2.0f * FarZ * MathF.Tan(0.5f * FovY);

            Proj = Matrix.PerspectiveFovLH(FovY, Aspect, NearZ, FarZ);
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

        public Plane[] FrustumPlanes => Frustum.Planes;

        public virtual bool Visible(BoundingBox box) {
            if (Frustum == null) throw new Exception("Call SetLens() first");
            return Frustum.Intersect(box) > 0;
        }

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            return Frustum.Intersect(box);
        }

        public Vector3[] GetFrustumCorners() {
            var hNear = 2 * MathF.Tan(FovY / 2) * NearZ;
            var wNear = hNear * Aspect;

            var hFar = 2 * MathF.Tan(FovY / 2) * FarZ;
            var wFar = hFar * Aspect;

            var cNear = Position + Look * NearZ;
            var cFar = Position + Look * FarZ;

            return new[] {
                //ntl
                cNear + (Up * hNear / 2) - (Right * wNear / 2),
                //ntr
                cNear + (Up * hNear / 2) + (Right * wNear / 2),
                //nbl
                cNear - (Up * hNear / 2) - (Right * wNear / 2),
                //nbr
                cNear - (Up * hNear / 2) + (Right * wNear / 2),
                //ftl
                cFar + (Up * hFar / 2) - (Right * wFar / 2),
                //ftr
                cFar + (Up * hFar / 2) + (Right * wFar / 2),
                //fbl
                cFar - (Up * hFar / 2) - (Right * wFar / 2),
                //fbr
                cFar - (Up * hFar / 2) + (Right * wFar / 2),
            };
        }
    }
}