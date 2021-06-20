using AcTools.Numerics;
using SlimDX;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5VectorUtils {
        public static Vector2 ToVector2(this Vec2 v) {
            return new Vector2(v.X, v.Y);
        }

        public static Vec2 ToVec2(this Vector2 v) {
            return new Vec2(v.X, v.Y);
        }
        public static Vector3 ToVector3(this Vec3 v) {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vec3 ToVec3(this Vector3 v) {
            return new Vec3(v.X, v.Y, v.Z);
        }

        public static Quaternion ToQuaternion(this Quat v) {
            return new Quaternion(v.X, v.Y, v.Z, v.W);
        }

        public static Quat ToQuat(this Quaternion v) {
            return new Quat(v.X, v.Y, v.Z, v.W);
        }

        public static Matrix ToMatrix(this Mat4x4 v) {
            Matrix ret;
            ret.M11 = v.M11;
            ret.M12 = v.M12;
            ret.M13 = v.M13;
            ret.M14 = v.M14;
            ret.M21 = v.M21;
            ret.M22 = v.M22;
            ret.M23 = v.M23;
            ret.M24 = v.M24;
            ret.M31 = v.M31;
            ret.M32 = v.M32;
            ret.M33 = v.M33;
            ret.M34 = v.M34;
            ret.M41 = v.M41;
            ret.M42 = v.M42;
            ret.M43 = v.M43;
            ret.M44 = v.M44;
            return ret;
        }

        public static Mat4x4 ToMat4x4(this Matrix v) {
            Mat4x4 ret;
            ret.M11 = v.M11;
            ret.M12 = v.M12;
            ret.M13 = v.M13;
            ret.M14 = v.M14;
            ret.M21 = v.M21;
            ret.M22 = v.M22;
            ret.M23 = v.M23;
            ret.M24 = v.M24;
            ret.M31 = v.M31;
            ret.M32 = v.M32;
            ret.M33 = v.M33;
            ret.M34 = v.M34;
            ret.M41 = v.M41;
            ret.M42 = v.M42;
            ret.M43 = v.M43;
            ret.M44 = v.M44;
            return ret;
        }
    }
}