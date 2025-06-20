﻿using AcTools.Render.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public class CameraOrtho : CameraBase {
        public float Width, Height;

        public Vector3 Target { get; set; }

        public CameraOrtho() : base(0) {
            Target = new Vector3();
            Up = new Vector3(0, 1, 0);
            Look = new Vector3(0, -1, 0);
            NearZ = 0.1f;
            FarZ = 500f;
        }

        public override void UpdateViewMatrix() {
            SetView(MatrixFix.LookAtRH(Position, Target, Up));
        }

        public virtual void Save() {
            throw new System.NotImplementedException();
        }

        public virtual void Restore() {
            throw new System.NotImplementedException();
        }

        public override CameraBase Clone() {
            return new CameraOrtho {
                Width = Width,
                Height = Height,
                Up = Up,
                Target = Target,
                Position = Position
            };
        }

        // ReSharper disable once OptionalParameterHierarchyMismatch
        public sealed override void SetLens(float aspect = 0f) {
            SetProj(MatrixFix.OrthoRH(Width, Height, NearZValue, FarZValue));
            UpdateViewMatrix();
        }

        protected override void LookAtOverride(Vector3 pos, Vector3 target, Vector3 up) {
            Position = pos;
            Target = target;
            Up = up;
        }

        public void Move(Vector3 d) {
            Target += d;
            Position += d;
        }

        public override void Strafe(float d) {
            Move(Right * d);
        }

        public override void Walk(float d) {
            Move(Up * d);
        }

        public override void Pitch(float angle) {}
        public override void Yaw(float angle) {}
        public override void Zoom(float dr) {}
    }
}