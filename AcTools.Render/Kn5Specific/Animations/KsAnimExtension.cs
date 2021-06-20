using AcTools.KsAnimFile;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Numerics;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Animations {
    // TODO: Properly interpolate quaterninons?
    public static class KsAnimExtension {
        public static KsAnimKeyframe ToKeyFrame(this Matrix matrix) {
            matrix.Decompose(out var scale, out var rotation, out var translation);
            return new KsAnimKeyframe(rotation.ToQuat(), translation.ToVec3(), scale.ToVec3());
        }

        [NotNull]
        private static Mat4x4[] ConvertFramesV1(Matrix[] matrices, int? fillLength) {
            var v = new Mat4x4[fillLength ?? matrices.Length];
            var l = Matrix.Identity.ToMat4x4();

            var i = 0;
            for (; i < matrices.Length; i++) {
                v[i] = l = matrices[i].ToMat4x4();
            }

            for (; i < v.Length; i++) {
                v[i] = l;
            }

            return v;
        }

        [NotNull]
        private static KsAnimKeyframe[] ConvertFramesV2(Matrix[] matrices, int? fillLength) {
            var v = new KsAnimKeyframe[fillLength ?? matrices.Length];
            var l = Matrix.Identity.ToKeyFrame();

            var i = 0;
            for (; i < matrices.Length; i++) {
                v[i] = l = matrices[i].ToKeyFrame();
            }

            for (; i < v.Length; i++) {
                v[i] = l;
            }

            return v;
        }

        public static void SetMatrices(this KsAnimEntryBase animEntry, Matrix[] matrices, int? fillLength = null) {
            if (animEntry is KsAnimEntryV2 v2) {
                v2.KeyFrames = ConvertFramesV2(matrices, fillLength);
            } else {
                ((KsAnimEntryV1)animEntry).Matrices = ConvertFramesV1(matrices, fillLength);
            }
        }

        [NotNull]
        public static Matrix[] GetMatrices(this KsAnimEntryBase animEntry) {
            return animEntry is KsAnimEntryV2 v2 ? ConvertFrames(v2.KeyFrames) : ConvertFrames(((KsAnimEntryV1)animEntry).Matrices);
        }

        private static Matrix ConvertFrame(KsAnimKeyframe ks) {
            var rotation = ks.Rotation.ToQuaternion();
            var translation = ks.Transition.ToVector3();
            var scale = ks.Scale.ToVector3();
            return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
        }

        [NotNull]
        private static Matrix[] ConvertFrames(KsAnimKeyframe[] ksAnimKeyframes) {
            var result = new Matrix[ksAnimKeyframes.Length];
            if (result.Length == 0) return result;

            var first = default(Matrix);
            var same = true;

            for (var i = 0; i < result.Length; i++) {
                var matrix = ConvertFrame(ksAnimKeyframes[i]);
                result[i] = matrix;

                if (i == 0) {
                    first = matrix;
                } else if (matrix != first) {
                    same = false;
                }
            }

            return same ? new[]{ first } : result;
        }

        [NotNull]
        private static Matrix[] ConvertFrames(Mat4x4[] matrices) {
            var result = new Matrix[matrices.Length];
            if (result.Length == 0) return result;

            var first = default(Matrix);
            var same = true;

            for (var i = 0; i < result.Length; i++) {
                var matrix = matrices[i].ToMatrix();
                result[i] = matrix;

                if (i == 0) {
                    first = matrix;
                }else if (matrix != first) {
                    same = false;
                }
            }

            return same ? new[]{ first } : result;
        }
    }
}