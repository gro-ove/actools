using AcTools.Kn5File;
using AcTools.Numerics;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5VertexUtils {
        public static Kn5Node.Vertex Transform(this Kn5Node.Vertex v, Mat4x4 m, double offsetAlongNormal = 0d) {
            var posVec = Vec3.Transform(v.Position, m);
            var normalVec = Vec3.TransformNormal(v.Normal, m);
            if (offsetAlongNormal != 0d) {
                posVec += Vec3.Normalize(normalVec) * (float)offsetAlongNormal;
            }
            v.Position = posVec;
            v.Normal = normalVec;
            v.Tangent = Vec3.TransformNormal(v.Tangent, m);
            return v;
        }
    }
}