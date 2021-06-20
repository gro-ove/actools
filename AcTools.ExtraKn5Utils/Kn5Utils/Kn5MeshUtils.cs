using System.Collections.Generic;
using AcTools.ExtraKn5Utils.ExtraMath;
using AcTools.Kn5File;
using AcTools.Numerics;

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
                BoundingSphereCenter = new Vec3(),
                CastShadows = true,
                Children = new List<Kn5Node>()
            };
        }

        public static Aabb3 CalculateAabb3(this Kn5Node mesh, Kn5Node relativeTo) {
            var transform = mesh.CalculateTransformRelativeToParent(relativeTo);
            var aabb = Aabb3.CreateNew();
            foreach (var v in mesh.Vertices) {
                aabb.Extend(Vec3.Transform(v.Position, transform));
            }
            return aabb;
        }

        public static void RecalculateTangents(this Kn5Node mesh) {
            var vertexCount = mesh.Vertices.Length;
            var triangleCount = mesh.Indices.Length / 3;
            var tan1 = new Vec3[vertexCount];
            var tan2 = new Vec3[vertexCount];
            for (long a = 0; a < triangleCount; a++) {
                var i1 = mesh.Indices[a * 3];
                var i2 = mesh.Indices[a * 3 + 1];
                var i3 = mesh.Indices[a * 3 + 2];
                var v1 = mesh.Vertices[i1];
                var v2 = mesh.Vertices[i2];
                var v3 = mesh.Vertices[i3];
                var x1 = v2.Position.X - v1.Position.X;
                var x2 = v3.Position.X - v1.Position.X;
                var y1 = v2.Position.Y - v1.Position.Y;
                var y2 = v3.Position.Y - v1.Position.Y;
                var z1 = v2.Position.Z - v1.Position.Z;
                var z2 = v3.Position.Z - v1.Position.Z;
                var s1 = v2.Tex.X - v1.Tex.X;
                var s2 = v3.Tex.X - v1.Tex.X;
                var t1 = v2.Tex.Y - v1.Tex.Y;
                var t2 = v3.Tex.Y - v1.Tex.Y;
                var r = 1f / (s1 * t2 - s2 * t1);
                var sdir = new Vec3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                var tdir = new Vec3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;
                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }
            for (long a = 0; a < vertexCount; a++) {
                mesh.Vertices[a].Tangent = Vec3.Normalize(tan1[a] - mesh.Vertices[a].Normal * Vec3.Dot(mesh.Vertices[a].Normal, tan1[a]));
            }
        }
    }
}