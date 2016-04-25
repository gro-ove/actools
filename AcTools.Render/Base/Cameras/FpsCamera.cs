using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public delegate float HeightFunc(float x, float y);

    public class FpsCamera : BaseCamera {
        public override void Save() {
            throw new System.NotImplementedException();
        }

        public override void Restore() {
            throw new System.NotImplementedException();
        }

        public FpsCamera(float fov) : base(fov) {}
        
        public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
            Position = pos;
            Look = Vector3.Normalize(target - pos);
            Right = Vector3.Normalize(Vector3.Cross(up, Look));
            Up = Vector3.Cross(Look, Right);
        }

        public override void Strafe(float d) {
            Position += Right * d;
        }

        public override void Walk(float d) {
            Position += Look * d;
        }

        public override void Pitch(float angle) {
            var r = Matrix.RotationAxis(Right, angle);
            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);
        }

        public override void Yaw(float angle) {
            var r = Matrix.RotationY(angle);
            Right = Vector3.TransformNormal(Right, r);
            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);
        }

        public override void Zoom(float dr) {
            FovY = MathF.Clamp(FovY + dr, 0.1f, MathF.PI / 2);
            SetLens(Aspect);
        }

        public override void UpdateViewMatrix() {
            var p = Position;
            var l = Vector3.Normalize(Look);
            var u = Vector3.Normalize(Vector3.Cross(l, Right));
            var r = Vector3.Cross(u, l);

            var x = -Vector3.Dot(p, r);
            var y = -Vector3.Dot(p, u);
            var z = -Vector3.Dot(p, l);

            Right = r;
            Up = u;
            Look = l;
            View = new Matrix {
                [0, 0] = Right.X,
                [1, 0] = Right.Y,
                [2, 0] = Right.Z,
                [3, 0] = x,
                [0, 1] = Up.X,
                [1, 1] = Up.Y,
                [2, 1] = Up.Z,
                [3, 1] = y,
                [0, 2] = Look.X,
                [1, 2] = Look.Y,
                [2, 2] = Look.Z,
                [3, 2] = z,
                [0, 3] = 0,
                [1, 3] = 0,
                [2, 3] = 0,
                [3, 3] = 1
            };

            Frustum = Frustum.FromViewProj(ViewProj);
        }
    }
}