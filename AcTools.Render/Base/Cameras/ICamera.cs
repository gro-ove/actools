using AcTools.Render.Base.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public interface ICamera {
        Vector3 Position { get; }

        Matrix ViewProj { get; }

        Matrix Proj { get; }

        Matrix View { get; }

        Matrix ViewProjInvert { get; }

        float FarZValue { get; }

        float NearZValue { get; }

        Vector3 Up { get; }

        Vector3 Right { get; }

        Vector3 Look { get; }

        bool Visible(BoundingBox box);

        FrustrumIntersectionType Intersect(BoundingBox box);
    }

    public static class CameraExtension {
        public static float GetOnScreenSize(this Vector3 worldPos, [CanBeNull] ICamera camera) {
            var viewProj = camera?.ViewProj;
            return viewProj != null ? (Vector3.TransformCoordinate(worldPos, viewProj.Value) -
                    Vector3.TransformCoordinate(worldPos + camera.Up, viewProj.Value)).Length() : 1f;
        }

        public static Matrix ToFixedSizeMatrix(this Matrix matrix, [CanBeNull] ICamera camera, float scale = 1f) {
            var viewProj = camera?.ViewProj;
            if (viewProj != null) {
                var worldPos = matrix.GetTranslationVector();
                var onScreenSize = (Vector3.TransformCoordinate(worldPos, viewProj.Value) -
                        Vector3.TransformCoordinate(worldPos + camera.Up, viewProj.Value)).Length();
                matrix = Matrix.Scaling(new Vector3(scale / onScreenSize)) * matrix;
            }

            return matrix;
        }

        public static Matrix ToFixedSizeMatrix(this Vector3 worldPos, [CanBeNull] ICamera camera, float scale = 1f) {
            var viewProj = camera?.ViewProj;

            var matrix = Matrix.Translation(worldPos);
            if (viewProj != null) {
                var onScreenSize = (Vector3.TransformCoordinate(worldPos, viewProj.Value) -
                        Vector3.TransformCoordinate(worldPos + camera.Up, viewProj.Value)).Length();
                matrix = Matrix.Scaling(new Vector3(scale / onScreenSize)) * matrix;
            }

            return matrix;
        }
    }
}