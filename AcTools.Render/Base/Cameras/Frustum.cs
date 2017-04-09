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

        private Frustum(Matrix vp) {
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

            for (var i = 0; i < 6; i++) {
                Planes[i].Normalize();
            }
        }

        public void Update(Matrix vp) {
            Planes[0].Normal = new Vector3(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31);
            Planes[0].D = vp.M44 + vp.M41;
            Planes[0].Normalize();

            Planes[1].Normal = new Vector3(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31);
            Planes[1].D = vp.M44 - vp.M41;
            Planes[1].Normalize();

            Planes[2].Normal = new Vector3(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32);
            Planes[2].D = vp.M44 + vp.M42;
            Planes[2].Normalize();

            Planes[3].Normal = new Vector3(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32);
            Planes[3].D = vp.M44 - vp.M42;
            Planes[3].Normalize();

            Planes[4].Normal = new Vector3(vp.M13, vp.M23, vp.M33);
            Planes[4].D = vp.M43;
            Planes[4].Normalize();

            Planes[5].Normal = new Vector3(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33);
            Planes[5].D = vp.M44 - vp.M43;
            Planes[5].Normalize();
        }

        public Plane[] Planes { get; }

        public static Frustum FromViewProj(Matrix viewProj) => new Frustum(viewProj);

        public FrustrumIntersectionType Intersect(BoundingBox box) {
            for (var i = 0; i < 6; i++) {
                switch (Plane.Intersects(Planes[i], box)) {
                    case PlaneIntersectionType.Back:
                        return FrustrumIntersectionType.None;
                    case PlaneIntersectionType.Intersecting:
                        return FrustrumIntersectionType.Intersection;
                }
            }

            return FrustrumIntersectionType.Inside;
        }

        public FrustrumIntersectionType IntersectOld(BoundingBox box) {
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