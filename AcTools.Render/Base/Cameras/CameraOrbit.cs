using AcTools.Render.Base.Utils;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public class CameraOrbit : BaseCamera {
        public float Radius, Alpha, Beta;
        public float MinBeta = float.MinValue, MinY = float.MinValue;

        public CameraOrbit(float fov) : base(fov) {
            Alpha = Beta = 0.5f;
            Radius = 10.0f;
            Target = new Vector3();
        }

        public Vector3 Target { get; set; }

        private float _radius, _alpha, _beta;
        private Vector3 _target;

        public override void Save() {
            _radius = Radius;
            _alpha = Alpha;
            _beta = Beta;
            _target = Target;
        }

        public override void Restore() {
            Radius = _radius;
            Alpha = _alpha;
            Beta = _beta;
            Target = _target;
        }

        public override BaseCamera Clone() {
            return new CameraOrbit(FovY) {
                Radius = Radius,
                Alpha = Alpha,
                Beta = Beta,
                Up = Up,
                Target = Target,
                Position = Position
            };
        }

        public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
            Target = target;
            Position = pos;

            var delta = target - pos;
            Radius = delta.Length();
            Look = Vector3.Normalize(delta);

            Alpha = MathF.AngleFromXY(-Look.X, -Look.Z);
            Beta = (-Look.Y).Asin();
        }

        public override void Strafe(float d) {
            var dt = Vector3.Normalize(new Vector3(Right.X, 0, Right.Z)) * d;
            Target += dt;
        }

        public override void Walk(float d) {
            Target += Vector3.Normalize(new Vector3(Look.X, 0, Look.Z)) * d;
        }

        public override void Pitch(float angle) {
            Beta += angle;
            Beta = Beta.Clamp(-0.508f, MathF.PI / 2.0f - 0.01f);
        }

        public override void Yaw(float angle) {
            Alpha = (Alpha + angle) % (MathF.PI * 2.0f);
        }

        public override void Zoom(float dr) {
            Radius = (Radius + dr).Clamp(1.2f, 1000f);
        }

        public override void UpdateViewMatrix() {
            if (Beta < MinBeta) {
                Beta = MinBeta;
            }

            var sideRadius = Radius * Beta.Cos();
            var height = Radius * Beta.Sin();

            if (Target.Y + height < MinY) {
                height = MinY - Target.Y;
            }

            Position = new Vector3(
                    Target.X + sideRadius * Alpha.Cos(),
                    Target.Y + height,
                    Target.Z + sideRadius * Alpha.Sin());

            SetView(Matrix.LookAtRH(Position, Target, Vector3.UnitY));

            Right = new Vector3(View.M11, View.M21, View.M31);
            Right.Normalize();

            Up = new Vector3(View.M12, View.M22, View.M32);
            Up.Normalize();

            Look = -new Vector3(View.M13, View.M23, View.M33);
            Look.Normalize();
        }
    }
}