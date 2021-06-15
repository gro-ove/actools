using System.Collections.Generic;
using AcTools.Kn5File;
using SlimDX;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5MeshUtils {
        public static Kn5Node Create(string name, uint materialId) {
            return new Kn5Node {
                Name = name,
                MaterialId = materialId,
                NodeClass = Kn5NodeClass.Mesh,
                Active = true,
                IsRenderable = true,
                IsVisible = true,
                BoundingSphereCenter = new float[3],
                CastShadows = true,
                Children = new List<Kn5Node>()
            };
        }

        public static void RecalculateTangents(this Kn5Node mesh) {
            var vertexCount = mesh.Vertices.Length;
            var triangleCount = mesh.Indices.Length / 3;
            var tan1 = new Vector3[vertexCount];
            var tan2 = new Vector3[vertexCount];
            for (long a = 0; a < triangleCount; a++) {
                var i1 = mesh.Indices[a * 3];
                var i2 = mesh.Indices[a * 3 + 1];
                var i3 = mesh.Indices[a * 3 + 2];
                var v1 = mesh.Vertices[i1];
                var v2 = mesh.Vertices[i2];
                var v3 = mesh.Vertices[i3];
                var x1 = v2.Position[0] - v1.Position[0];
                var x2 = v3.Position[0] - v1.Position[0];
                var y1 = v2.Position[1] - v1.Position[1];
                var y2 = v3.Position[1] - v1.Position[1];
                var z1 = v2.Position[2] - v1.Position[2];
                var z2 = v3.Position[2] - v1.Position[2];
                var s1 = v2.TexC[0] - v1.TexC[0];
                var s2 = v3.TexC[0] - v1.TexC[0];
                var t1 = v2.TexC[1] - v1.TexC[1];
                var t2 = v3.TexC[1] - v1.TexC[1];
                var r = 1.0F / (s1 * t2 - s2 * t1);
                var sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;
                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }
            for (long a = 0; a < vertexCount; a++) {
                var n = new Vector3(mesh.Vertices[a].Normal[0], mesh.Vertices[a].Normal[1], mesh.Vertices[a].Normal[2]);
                var t = tan1[a];
                var v = Vector3.Normalize(t - n * Vector3.Dot(n, t));
                mesh.Vertices[a].TangentU = new[] { v.X, v.Y, v.Z };
            }
        }
    }
}