using SlimDX;

namespace AcTools.Kn5Render.Kn5Render {
    public class CameraOrbit  : CameraBase {
        public float Radius, Alpha, Beta;

        public CameraOrbit(float fov) : base(fov){
            Alpha = Beta = 0.5f;
            Radius = 10.0f;
            _target = new Vector3();
        }

        private Vector3 _target;

        public Vector3 Target {
            get { return _target; }
            set {
                _target = value;
                Moved = true;
            }
        }

        private float _sRadius, _sAlpha, _sBeta;
        private Vector3 _sTarget;

        public override void Save() {
            _sRadius = Radius;
            _sAlpha = Alpha;
            _sBeta = Beta;
            _sTarget = Target;
        }

        public override void Restore() {
            Radius = _sRadius;
            Alpha = _sAlpha;
            Beta = _sBeta;
            Target = _sTarget;
        }

        public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
            _target = target;
            Position = pos;
            Look = Vector3.Normalize(target - pos);
            Right = Vector3.Normalize(Vector3.Cross(up, Look));
            Up = Vector3.Cross(Look, Right);
            Radius = (target - pos).Length();
        }

        public override void Strafe(float d) {
            var dt = Vector3.Normalize(new Vector3(Right.X, 0, Right.Z)) * d;
            _target += dt;
            Moved = true;
        }

        public override void Walk(float d) {
            _target += Vector3.Normalize(new Vector3(Look.X, 0, Look.Z)) *d;
            Moved = true;
        }

        public override void Pitch(float angle) {
            Beta += angle;
            Beta = MathF.Clamp(Beta, -0.08f, MathF.PI/2.0f - 0.01f);
            Moved = true;
        }

        public override void Yaw(float angle) {
            Alpha = (Alpha + angle) % (MathF.PI*2.0f);
            Moved = true;
        }

        public override void Zoom(float dr) {
            Radius += dr;
            Radius = MathF.Clamp(Radius, 1.2f, 150.0f);
            Moved = true;
        }

        public override void UpdateViewMatrix() {
            var sideRadius = Radius*MathF.Cos(Beta);
            var height = Radius*MathF.Sin(Beta);

            Position = new Vector3(
                _target.X + sideRadius * MathF.Cos(Alpha), 
                _target.Y + height, 
                _target.Z + sideRadius * MathF.Sin(Alpha) 
            );

            View = Matrix.LookAtLH(Position, _target, Vector3.UnitY);

            Right = new Vector3(View.M11, View.M21, View.M31);
            Right.Normalize();

            Look = new Vector3(View.M13, View.M23, View.M33);
            Look.Normalize();
        }
    }
}