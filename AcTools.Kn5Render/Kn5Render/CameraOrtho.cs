using SlimDX;

namespace AcTools.Kn5Render.Kn5Render {
    public class CameraOrtho : CameraBase {
        public float Width, Height;

        public Vector3 Target { get; set; }

        public CameraOrtho()
            : base(0) {
            Target = new Vector3();
            Up = new Vector3(0, 0, 1);
            Look = new Vector3(0, -1, 0);
        }
        public override void UpdateViewMatrix() {
            View = Matrix.LookAtLH(Position, Target, Up);
        }

        public override void Save() {
            //throw new System.NotImplementedException();
        }

        public override void Restore() {
            //throw new System.NotImplementedException();
        }

        public override void SetLens(float aspect) {
            Proj = Matrix.OrthoLH(Width, Height, NearZ, FarZ);
            UpdateViewMatrix();
        }

        public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) { }
        public override void Strafe(float d) { }
        public override void Walk(float d) { }
        public override void Pitch(float angle) { }
        public override void Yaw(float angle) { }
        public override void Zoom(float dr) { }
    }
}