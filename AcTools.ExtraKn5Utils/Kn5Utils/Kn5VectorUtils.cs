using SlimDX;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5VectorUtils {
        public static Vector3 ToVector3(this float[] v) {
            return new Vector3(v[0], v[1], v[2]);
        }

        public static float[] ToFloatArray(this Vector3 v) {
            return new[] { v.X, v.Y, v.Z };
        }

        public static Matrix ToMatrix(this float[] v) {
            var m = new Matrix();
            for (var i = 0; i < 16; ++i) {
                m[i / 4, i % 4] = v[i];
            }
            return m;
        }

        public static float[] ToFloatArray(this Matrix m) {
            var v = new float[16];
            for (var i = 0; i < 16; ++i) {
                v[i] = m[i / 4, i % 4];
            }
            return v;
        }
    }
}