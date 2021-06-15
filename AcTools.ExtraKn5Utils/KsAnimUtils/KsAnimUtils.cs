using System;
using System.Linq;
using AcTools.KsAnimFile;

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

        private static bool IsFrameSame(float[] a, float[] b) {
            for (var i = a.Length - 1; i >= 0; --i) {
                if (Math.Abs(a[i] - b[i]) > 0.0001f) return false;
            }
            return true;
        }

        private static bool IsFrameSame(KsAnimKeyframe a, KsAnimKeyframe b) {
            return IsFrameSame(a.Transition, b.Transition) && IsFrameSame(a.Rotation, b.Rotation) && IsFrameSame(a.Scale, b.Scale);
        }
    }
}