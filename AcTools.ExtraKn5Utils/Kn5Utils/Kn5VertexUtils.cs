using AcTools.Kn5File;
using SlimDX;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5VertexUtils {
        public static Kn5Node.Vertex Transform(this Kn5Node.Vertex v, Matrix m) {
            v.Position = Vector3.TransformCoordinate(v.Position.ToVector3(), m).ToFloatArray();
            v.Normal = Vector3.TransformNormal(v.Normal.ToVector3(), m).ToFloatArray();
            v.TangentU = Vector3.TransformNormal(v.TangentU.ToVector3(), m).ToFloatArray();
            return v;
        }
    }
}