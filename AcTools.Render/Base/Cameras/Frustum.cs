using System.Linq;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public class Frustum {
        public const int Left = 0;
        public const int Right = 1;
        public const int Bottom = 2;
        public const int Top = 3;
        public const int Near = 4;
        public const int Far = 5;

        public Frustum(Matrix vp) {
            Planes = new[] {
                //left
                new Plane(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41),
                // right
                new Plane(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41),
                // bottom
                new Plane(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42),
                // top
                new Plane(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42),
                //near
                new Plane(vp.M13, vp.M23, vp.M33, vp.M43),
                //far
                new Plane(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43)
            };

            foreach (var plane in Planes) {
                plane.Normalize();
            }
        }

        public Plane[] Planes { get; }

        public static Frustum FromViewProj(Matrix viewProj) => new Frustum(viewProj);

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            var totalIn = 0;

            foreach (var intersection in Planes.Select(plane => Plane.Intersects(plane, box))) {
                switch (intersection) {
                    case PlaneIntersectionType.Back:
                        return FrustrumIntersectionType.None;
                    case PlaneIntersectionType.Front:
                        totalIn++;
                        break;
                }
            }

            return totalIn == 6 ? FrustrumIntersectionType.Inside : FrustrumIntersectionType.Intersection;
        }
    }
}