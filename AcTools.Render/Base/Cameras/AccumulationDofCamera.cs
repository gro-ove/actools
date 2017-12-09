using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public class AccumulationDofCamera : FpsCamera {
        public bool SkewMode { get; set; } = true;

        public AccumulationDofCamera(float fov) : base(fov) {}

        public override CameraBase Clone() {
            return new AccumulationDofCamera(FovY) {
                Position = Position,
                Look = Look,
                Right = Right,
                Up = Up,
                Tilt = Tilt
            };
        }

        public float FocusPlane;
        public Vector2 ApertureOffset;

        private void UpdateViewMatrixRotate() {
            var newPosition = Position + Right * ApertureOffset.X + Up * ApertureOffset.Y;
            var lookAt = Position + Look * FocusPlane;
            SetView(RhMode ? Matrix.LookAtRH(newPosition, lookAt, GetUpTilt(lookAt, Up)) :
                    Matrix.LookAtLH(newPosition, lookAt, GetUpTilt(lookAt, Up)));
            Right = Vector3.Normalize(new Vector3(View.M11, View.M21, View.M31));
        }

        public override void SetLens(float aspect) {
            if (SkewMode) {
                Aspect = aspect;

                var proj = RhMode ? Matrix.PerspectiveFovRH(FovY, Aspect, NearZValue, FarZValue) :
                        Matrix.PerspectiveFovLH(FovY, Aspect, NearZValue, FarZValue);
                proj.M31 -= ApertureOffset.X * proj.M11 / FocusPlane;
                proj.M32 -= ApertureOffset.Y * proj.M22 / FocusPlane;
                SetProj(proj);
            } else {
                base.SetLens(aspect);
            }
        }

        private void UpdateViewMatrixSkew() {
            var target = Position + Look;
            var matrix = RhMode ? Matrix.LookAtRH(Position, target, GetUpTilt(target, Up)) :
                    Matrix.LookAtLH(Position, target, GetUpTilt(target, Up));

            var right = new Vector3(matrix.M11, matrix.M21, matrix.M31);
            var up = new Vector3(matrix.M12, matrix.M22, matrix.M32);
            // var look = new Vector3(matrix.M13, matrix.M23, matrix.M33);

            var offset = right * ApertureOffset.X + up * ApertureOffset.Y;
            matrix.M41 -= Vector3.Dot(right, offset);
            matrix.M42 -= Vector3.Dot(up, offset);
            // matrix.M43 -= Vector3.Dot(look, offset);

            SetView(matrix);
            Right = Vector3.Normalize(right);
        }

        public override void UpdateViewMatrix() {
            if (SkewMode) {
                UpdateViewMatrixSkew();
            } else {
                UpdateViewMatrixRotate();
            }
        }
    }
}