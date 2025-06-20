﻿using AcTools.Render.Utils;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public abstract class CameraBase : ICamera {
        public bool RhMode { get; set; } = true;

        public Vector3 Position { get; set; }
        public Vector3 Right;
        public Vector3 Up;
        public Vector3 Look;
        public float? NearZ;
        public float? FarZ;
        public float Aspect;
        public float FovY;
        public float Tilt;

        private float _nearZFovY, _nearZCached;

        public float NearZValue {
            get {
                if (NearZ.HasValue) return NearZ.Value;
                if (Equals(_nearZFovY, FovY)) return _nearZCached;
                _nearZFovY = FovY;
                return _nearZCached = 0.04f / _nearZFovY.Pow(2f);
            }
        }

        private float _farZFovY, _farZCached;

        public float FarZValue {
            get {
                if (FarZ.HasValue) return FarZ.Value;
                if (Equals(_farZFovY, FovY)) return _farZCached;
                _farZFovY = FovY;
                return _farZCached = 1e3f / _farZFovY.Pow(0.5f);
            }
        }

        public Matrix View { get; private set; }

        protected void SetView(Matrix view) {
            if (view.Equals(View)) return;
            View = view;

            _viewProj = null;
            _viewProjInvert = null;
            _frustumOld = true;
        }

        public Matrix Proj { get; private set; }

        protected void SetProj(Matrix proj) {
            if (CutProj != null) {
                proj = proj * CutProj.Value;
            }

            if (proj.Equals(Proj)) return;
            Proj = proj;

            _viewProj = null;
            _viewProjInvert = null;
            _frustumOld = true;
        }

        public Matrix? CutProj { get; set; }

        public Matrix ViewProj => _viewProj ?? (_viewProj = View * Proj).Value;
        private Matrix? _viewProj;

        public Matrix ViewProjInvert => _viewProjInvert ?? (_viewProjInvert = ViewProj.Invert_v2()).Value;
        private Matrix? _viewProjInvert;

        protected CameraBase(float fov) {
            Position = new Vector3();
            Right = new Vector3(1, 0, 0);
            Up = new Vector3(0, 1, 0);
            Look = new Vector3(0, 0, 1);
            FovY = fov;

            SetView(Matrix.Identity);
            SetProj(Matrix.Identity);
        }

        public abstract void Strafe(float d);
        public abstract void Walk(float d);
        public abstract void Pitch(float angle);
        public abstract void Yaw(float angle);
        public abstract void Zoom(float dr);
        public abstract void UpdateViewMatrix();
        public abstract CameraBase Clone();

        public void LookAt(Vector3 pos, Vector3 target, float tilt) {
            Tilt = tilt;
            LookAtOverride(pos, target, Vector3.UnitY);
        }

        public void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
            Tilt = 0f;
            LookAtOverride(pos, target, up);
        }

        protected Vector3 GetUpTilt(Vector3 target, Vector3 defaultValue) {
            return Tilt == 0f ? defaultValue :
                    Vector3.TransformNormal(Vector3.UnitY, Matrix.RotationAxis(Vector3.Normalize(target - Position), Tilt));
        }

        protected abstract void LookAtOverride(Vector3 pos, Vector3 target, Vector3 up);

        public virtual void SetLens(float aspect) {
            Aspect = aspect;
            SetProj(RhMode ? MatrixFix.PerspectiveFovRH(FovY, Aspect, NearZValue, FarZValue) :
                    MatrixFix.PerspectiveFovLH(FovY, Aspect, NearZValue, FarZValue));
        }

        public Ray GetPickingRay(Vector2 sp, Vector2 screenDims) {
            var screen = new Vector3(2.0f * sp.X / screenDims.X - 1.0f, 1.0f - 2.0f * sp.Y / screenDims.Y, 0.5f);
            var world = Vector3.TransformCoordinate(screen, ViewProjInvert);
            return new Ray(Position, Vector3.Normalize(world - Position));
        }

        private Frustum _frustum;
        private bool _frustumOld;

        [NotNull]
        protected Frustum Frustum {
            get {
                if (_frustum == null) {
                    _frustum = Frustum.FromViewProj(ViewProj);
                    _frustumOld = false;
                }

                if (_frustumOld) {
                    _frustum.Update(ViewProj);
                    _frustumOld = false;
                }

                return _frustum;
            }
        }

        public bool DisableFrustum { get; set; }

        public virtual bool Visible(BoundingBox box) {
            return DisableFrustum || Frustum.Intersect(box) > 0;
        }

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            return Frustum.Intersect(box);
        }

        Vector3 ICamera.Up => Up;
        Vector3 ICamera.Right => Right;
        Vector3 ICamera.Look => Look;
    }
}