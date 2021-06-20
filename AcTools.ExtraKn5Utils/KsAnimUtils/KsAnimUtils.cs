using System;
using System.Linq;
using AcTools.KsAnimFile;
using AcTools.Numerics;

namespace AcTools.ExtraKn5Utils.KsAnimUtils {
    public static class KsAnimUtils {
        public static bool IsStatic(this KsAnimEntryBase entry) {
            switch (entry) {
                case KsAnimEntryV1 v1:
                    return v1.Matrices.Length < 2 || v1.Matrices.All(x => IsFrameSame(x, v1.Matrices[0]));
                case KsAnimEntryV2 v2:
                    return v2.KeyFrames.Length < 2 || v2.KeyFrames.All(x => IsFrameSame(x, v2.KeyFrames[0]));
                default:
                    return true;
            }
        }

        private static bool IsFrameSame(Vec3 a, Vec3 b) {
            return Math.Abs(a.X - b.X) < 0.0001f
                    && Math.Abs(a.Y - b.Y) < 0.0001f
                    && Math.Abs(a.Z - b.Z) < 0.0001f;
        }

        private static bool IsFrameSame(Quat a, Quat b) {
            return Math.Abs(a.X - b.X) < 0.0001f
                    && Math.Abs(a.Y - b.Y) < 0.0001f
                    && Math.Abs(a.Z - b.Z) < 0.0001f
                    && Math.Abs(a.W - b.W) < 0.0001f;
        }

        private static bool IsFrameSame(Mat4x4 a, Mat4x4 b) {
            return Math.Abs(a.M11 - b.M11) < 0.0001f
                    && Math.Abs(a.M12 - b.M12) < 0.0001f
                    && Math.Abs(a.M13 - b.M13) < 0.0001f
                    && Math.Abs(a.M14 - b.M14) < 0.0001f
                    && Math.Abs(a.M21 - b.M21) < 0.0001f
                    && Math.Abs(a.M22 - b.M22) < 0.0001f
                    && Math.Abs(a.M23 - b.M23) < 0.0001f
                    && Math.Abs(a.M24 - b.M24) < 0.0001f
                    && Math.Abs(a.M31 - b.M31) < 0.0001f
                    && Math.Abs(a.M32 - b.M32) < 0.0001f
                    && Math.Abs(a.M33 - b.M33) < 0.0001f
                    && Math.Abs(a.M34 - b.M34) < 0.0001f
                    && Math.Abs(a.M41 - b.M41) < 0.0001f
                    && Math.Abs(a.M42 - b.M42) < 0.0001f
                    && Math.Abs(a.M43 - b.M43) < 0.0001f
                    && Math.Abs(a.M44 - b.M44) < 0.0001f;
        }

        private static bool IsFrameSame(KsAnimKeyframe a, KsAnimKeyframe b) {
            return IsFrameSame(a.Transition, b.Transition) && IsFrameSame(a.Rotation, b.Rotation) && IsFrameSame(a.Scale, b.Scale);
        }
    }
}