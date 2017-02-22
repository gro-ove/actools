using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SlimDX;

namespace AcTools.Render.Base.Utils {
    public static class GeometryGenerator {
        private static readonly List<Vector3> IcosahedronVertices = new List<Vector3> {
            new Vector3(-0.525731f, 0, 0.850651f),
            new Vector3(0.525731f, 0, 0.850651f),
            new Vector3(-0.525731f, 0, -0.850651f),
            new Vector3(0.525731f, 0, -0.850651f),
            new Vector3(0, 0.850651f, 0.525731f),
            new Vector3(0, 0.850651f, -0.525731f),
            new Vector3(0, -0.850651f, 0.525731f),
            new Vector3(0, -0.850651f, -0.525731f),
            new Vector3(0.850651f, 0.525731f, 0),
            new Vector3(-0.850651f, 0.525731f, 0),
            new Vector3(0.850651f, -0.525731f, 0),
            new Vector3(-0.850651f, -0.525731f, 0)
        };

        private static readonly List<ushort> IcosahedronIndices = new List<ushort> {
            1,
            4,
            0,
            4,
            9,
            0,
            4,
            5,
            9,
            8,
            5,
            4,
            1,
            8,
            4,
            1,
            10,
            8,
            10,
            3,
            8,
            8,
            3,
            5,
            3,
            2,
            5,
            3,
            7,
            2,
            3,
            10,
            7,
            10,
            6,
            7,
            6,
            11,
            7,
            6,
            0,
            11,
            6,
            1,
            0,
            10,
            1,
            6,
            11,
            0,
            9,
            2,
            11,
            9,
            5,
            2,
            9,
            11,
            2,
            7
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex {
            public Vector3 Position { get; set; }

            public Vector3 Normal { get; set; }

            public Vector3 TangentU { get; set; }

            public Vector2 TexC { get; set; }

            public Vertex(Vector3 pos, Vector3 norm, Vector3 tan, Vector2 uv) {
                Position = pos;
                Normal = norm;
                TangentU = tan;
                TexC = uv;
            }

            public Vertex(float px, float py, float pz, float nx, float ny, float nz, float tx, float ty, float tz, float u, float v) :
                    this(new Vector3(px, py, pz), new Vector3(nx, ny, nz), new Vector3(tx, ty, tz), new Vector2(u, v)) {}
        }

        public class MeshData {
            public List<Vertex> Vertices = new List<Vertex>();
            public List<ushort> Indices = new List<ushort>();
        }

        public static MeshData CreateBox(Vector3 size) {
            return CreateBox(size.X, size.Y, size.Z);
        }

        public static MeshData CreateBox(float width, float height, float depth) {
            var ret = new MeshData();

            var w2 = 0.5f * width;
            var h2 = 0.5f * height;
            var d2 = 0.5f * depth;

            // front
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 1, 1));

            // back
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 1, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 1, 0));

            // top
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 1, 1));

            // bottom
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 1, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 1, 0));

            // left
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, -1, 0, 0, 0, 0, -1, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, -1, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, -1, 0, 0, 0, 0, -1, 1, 0));
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, -1, 0, 0, 0, 0, -1, 1, 1));

            // right
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 1, 0, 0, 0, 0, 1, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 1, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 1, 0, 0, 0, 0, 1, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 1, 0, 0, 0, 0, 1, 1, 1));

            ret.Indices.AddRange(new ushort[] {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9, 10, 8, 10, 11,
                12, 13, 14, 12, 14, 15,
                16, 17, 18, 16, 18, 19,
                20, 21, 22, 20, 22, 23
            });

            return ret;
        }

        public static MeshData CreateLinesBox(Vector3 size) {
            return CreateLinesBox(size.X, size.Y, size.Z);
        }

        public static MeshData CreateLinesBox(float width, float height, float depth) {
            var ret = new MeshData();

            var w2 = 0.5f * width;
            var h2 = 0.5f * height;
            var d2 = 0.5f * depth;

            // front
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 0, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, 0, -1, 1, 0, 0, 1, 1));

            // back
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 1, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, 0, 1, -1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 0, 1, -1, 0, 0, 1, 0));

            // top
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 0, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 0, 1, 0, 1, 0, 0, 1, 1));

            // bottom
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 1, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 0, -1, 0, -1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, 0, -1, 0, -1, 0, 0, 1, 0));

            // left
            ret.Vertices.Add(new Vertex(-w2, -h2, +d2, -1, 0, 0, 0, 0, -1, 0, 1));
            ret.Vertices.Add(new Vertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, -1, 0, 0));
            ret.Vertices.Add(new Vertex(-w2, +h2, -d2, -1, 0, 0, 0, 0, -1, 1, 0));
            ret.Vertices.Add(new Vertex(-w2, -h2, -d2, -1, 0, 0, 0, 0, -1, 1, 1));

            // right
            ret.Vertices.Add(new Vertex(+w2, -h2, -d2, 1, 0, 0, 0, 0, 1, 0, 1));
            ret.Vertices.Add(new Vertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 1, 0, 0));
            ret.Vertices.Add(new Vertex(+w2, +h2, +d2, 1, 0, 0, 0, 0, 1, 1, 0));
            ret.Vertices.Add(new Vertex(+w2, -h2, +d2, 1, 0, 0, 0, 0, 1, 1, 1));

            ret.Indices.AddRange(new ushort[] {
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                8, 9, 9, 10, 10, 11, 11, 8,
                12, 13, 13, 14, 14, 15, 15, 12,
                16, 17, 17, 18, 18, 19, 19, 16,
                20, 21, 21, 22, 22, 23, 23, 20
            });

            return ret;
        }

        public static MeshData CreateSphere(float radius, int sliceCount, int stackCount) {
            var ret = new MeshData();
            ret.Vertices.Add(new Vertex(0, radius, 0, 0, 1, 0, 1, 0, 0, 0, 0));
            var phiStep = MathF.PI / stackCount;
            var thetaStep = 2.0f * MathF.PI / sliceCount;

            for (var i = 1; i <= stackCount - 1; i++) {
                var phi = i * phiStep;
                for (var j = 0; j <= sliceCount; j++) {
                    var theta = j * thetaStep;
                    var p = new Vector3(
                            (radius * MathF.Sin(phi) * MathF.Cos(theta)),
                            (radius * MathF.Cos(phi)),
                            (radius * MathF.Sin(phi) * MathF.Sin(theta))
                            );

                    var t = new Vector3(-radius * MathF.Sin(phi) * MathF.Sin(theta), 0, radius * MathF.Sin(phi) * MathF.Cos(theta));
                    t.Normalize();
                    var n = Vector3.Normalize(p);

                    var uv = new Vector2(theta / (MathF.PI * 2), phi / MathF.PI);
                    ret.Vertices.Add(new Vertex(p, n, t, uv));
                }
            }
            ret.Vertices.Add(new Vertex(0, -radius, 0, 0, -1, 0, 1, 0, 0, 0, 1));


            for (var i = 1; i <= sliceCount; i++) {
                ret.Indices.Add(0);
                ret.Indices.Add((ushort)(i + 1));
                ret.Indices.Add((ushort)i);
            }
            var baseIndex = 1;
            var ringVertexCount = sliceCount + 1;
            for (var i = 0; i < stackCount - 2; i++) {
                for (var j = 0; j < sliceCount; j++) {
                    ret.Indices.Add((ushort)(baseIndex + i * ringVertexCount + j));
                    ret.Indices.Add((ushort)(baseIndex + i * ringVertexCount + j + 1));
                    ret.Indices.Add((ushort)(baseIndex + (i + 1) * ringVertexCount + j));

                    ret.Indices.Add((ushort)(baseIndex + (i + 1) * ringVertexCount + j));
                    ret.Indices.Add((ushort)(baseIndex + i * ringVertexCount + j + 1));
                    ret.Indices.Add((ushort)(baseIndex + (i + 1) * ringVertexCount + j + 1));
                }
            }
            var southPoleIndex = ret.Vertices.Count - 1;
            baseIndex = southPoleIndex - ringVertexCount;
            for (var i = 0; i < sliceCount; i++) {
                ret.Indices.Add((ushort)southPoleIndex);
                ret.Indices.Add((ushort)(baseIndex + i));
                ret.Indices.Add((ushort)(baseIndex + i + 1));
            }
            return ret;
        }

        public enum SubdivisionCount {
            None = 0,
            One = 1,
            Two = 2,
            Three = 3,
            Four = 4,
            Five = 5,
            Six = 6,
            Seven = 7,
            Eight = 8
        }

        public static MeshData CreateGeosphere(float radius, SubdivisionCount numSubdivisions) {
            var tempMesh = new MeshData {
                Vertices = IcosahedronVertices.Select(p => new Vertex { Position = p }).ToList(),
                Indices = IcosahedronIndices
            };

            var mh = new Subdivider();

            for (var i = 0; i < (int)numSubdivisions; i++) {
                mh.Subdivide4(tempMesh);
            }

            // Project vertices onto sphere and scale.
            for (var i = 0; i < tempMesh.Vertices.Count; i++) {
                // Project onto unit sphere.
                var n = Vector3.Normalize(tempMesh.Vertices[i].Position);
                // Project onto sphere.
                var p = radius * n;

                // Derive texture coordinates from spherical coordinates.
                var theta = MathF.AngleFromXY(tempMesh.Vertices[i].Position.X, tempMesh.Vertices[i].Position.Z);
                var phi = MathF.Acos(tempMesh.Vertices[i].Position.Y / radius);
                var texC = new Vector2(theta / (2 * MathF.PI), phi / MathF.PI);

                // Partial derivative of P with respect to theta
                var tangent = new Vector3(
                        -radius * MathF.Sin(phi) * MathF.Sin(theta),
                        0,
                        radius * MathF.Sin(phi) * MathF.Cos(theta)
                        );
                tangent.Normalize();

                tempMesh.Vertices[i] = new Vertex(p, n, tangent, texC);
            }
            return tempMesh;
        }

        private class Subdivider {
            private List<Vertex> _vertices;
            private List<ushort> _indices;
            private Dictionary<Tuple<int, int>, ushort> _newVertices;

            public void Subdivide4(MeshData mesh) {
                _newVertices = new Dictionary<Tuple<int, int>, ushort>();
                _vertices = mesh.Vertices;
                _indices = new List<ushort>();
                var numTris = mesh.Indices.Count / 3;

                for (var i = 0; i < numTris; i++) {
                    //       i2
                    //       *
                    //      / \
                    //     /   \
                    //   a*-----*b
                    //   / \   / \
                    //  /   \ /   \
                    // *-----*-----*
                    // i1    c      i3

                    var i1 = mesh.Indices[i * 3];
                    var i2 = mesh.Indices[i * 3 + 1];
                    var i3 = mesh.Indices[i * 3 + 2];

                    ushort a = GetNewVertex(i1, i2);
                    ushort b = GetNewVertex(i2, i3);
                    ushort c = GetNewVertex(i3, i1);

                    _indices.AddRange(new ushort[] {
                        i1, a, c,
                        i2, b, a,
                        i3, c, b,
                        a, b, c
                    });
                }

                mesh.Indices = _indices;
            }

            private ushort GetNewVertex(int i1, int i2) {
                var t1 = new Tuple<int, int>(i1, i2);
                var t2 = new Tuple<int, int>(i2, i1);

                if (_newVertices.ContainsKey(t2)) {
                    return _newVertices[t2];
                }
                if (_newVertices.ContainsKey(t1)) {
                    return _newVertices[t1];
                }
                var newIndex = (ushort)_vertices.Count;
                _newVertices.Add(t1, newIndex);

                _vertices.Add(new Vertex { Position = (_vertices[i1].Position + _vertices[i2].Position) * 0.5f });

                return newIndex;
            }
        }

        public static MeshData CreateCylinder(float bottomRadius, float topRadius, float height, int sliceCount, int stackCount) {
            if (sliceCount <= 0) throw new ArgumentOutOfRangeException(nameof(sliceCount));

            var ret = new MeshData();

            var stackHeight = height / stackCount;
            var radiusStep = (topRadius - bottomRadius) / stackCount;
            var ringCount = stackCount + 1;

            for (var i = 0; i < ringCount; i++) {
                var y = -0.5f * height + i * stackHeight;
                var r = bottomRadius + i * radiusStep;
                var dTheta = 2.0f * MathF.PI / sliceCount;
                for (var j = 0; j <= sliceCount; j++) {

                    var c = MathF.Cos(j * dTheta);
                    var s = MathF.Sin(j * dTheta);

                    var v = new Vector3(r * c, y, r * s);
                    var uv = new Vector2((float)j / sliceCount, 1.0f - (float)i / stackCount);
                    var t = new Vector3(-s, 0.0f, c);

                    var dr = bottomRadius - topRadius;
                    var bitangent = new Vector3(dr * c, -height, dr * s);

                    var n = Vector3.Normalize(Vector3.Cross(t, bitangent));

                    ret.Vertices.Add(new Vertex(v, n, t, uv));

                }
            }
            var ringVertexCount = sliceCount + 1;
            for (var i = 0; i < stackCount; i++) {
                for (var j = 0; j < sliceCount; j++) {
                    ret.Indices.Add((ushort)(i * ringVertexCount + j));
                    ret.Indices.Add((ushort)((i + 1) * ringVertexCount + j));
                    ret.Indices.Add((ushort)((i + 1) * ringVertexCount + j + 1));

                    ret.Indices.Add((ushort)(i * ringVertexCount + j));
                    ret.Indices.Add((ushort)((i + 1) * ringVertexCount + j + 1));
                    ret.Indices.Add((ushort)(i * ringVertexCount + j + 1));
                }
            }
            BuildCylinderTopCap(topRadius, height, sliceCount, ref ret);
            BuildCylinderBottomCap(bottomRadius, height, sliceCount, ref ret);
            return ret;
        }

        private static void BuildCylinderTopCap(float topRadius, float height, int sliceCount, ref MeshData ret) {
            var baseIndex = ret.Vertices.Count;

            var y = 0.5f * height;
            var dTheta = 2.0f * MathF.PI / sliceCount;

            for (var i = 0; i <= sliceCount; i++) {
                var x = topRadius * MathF.Cos(i * dTheta);
                var z = topRadius * MathF.Sin(i * dTheta);

                var u = x / height + 0.5f;
                var v = z / height + 0.5f;
                ret.Vertices.Add(new Vertex(x, y, z, 0, 1, 0, 1, 0, 0, u, v));
            }
            ret.Vertices.Add(new Vertex(0, y, 0, 0, 1, 0, 1, 0, 0, 0.5f, 0.5f));
            var centerIndex = ret.Vertices.Count - 1;
            for (var i = 0; i < sliceCount; i++) {
                ret.Indices.Add((ushort)centerIndex);
                ret.Indices.Add((ushort)(baseIndex + i + 1));
                ret.Indices.Add((ushort)(baseIndex + i));
            }
        }

        private static void BuildCylinderBottomCap(float bottomRadius, float height, int sliceCount, ref MeshData ret) {
            var baseIndex = ret.Vertices.Count;

            var y = -0.5f * height;
            var dTheta = 2.0f * MathF.PI / sliceCount;

            for (var i = 0; i <= sliceCount; i++) {
                var x = bottomRadius * MathF.Cos(i * dTheta);
                var z = bottomRadius * MathF.Sin(i * dTheta);

                var u = x / height + 0.5f;
                var v = z / height + 0.5f;
                ret.Vertices.Add(new Vertex(x, y, z, 0, -1, 0, 1, 0, 0, u, v));
            }
            ret.Vertices.Add(new Vertex(0, y, 0, 0, -1, 0, 1, 0, 0, 0.5f, 0.5f));
            var centerIndex = ret.Vertices.Count - 1;
            for (var i = 0; i < sliceCount; i++) {
                ret.Indices.Add((ushort)centerIndex);
                ret.Indices.Add((ushort)(baseIndex + i));
                ret.Indices.Add((ushort)(baseIndex + i + 1));
            }
        }

        public static MeshData CreateGrid(float width, float depth, int m, int n) {
            var ret = new MeshData();

            var halfWidth = width * 0.5f;
            var halfDepth = depth * 0.5f;

            var dx = width / (n - 1);
            var dz = depth / (m - 1);

            var du = 1.0f / (n - 1);
            var dv = 1.0f / (m - 1);

            for (var i = 0; i < m; i++) {
                var z = halfDepth - i * dz;
                for (var j = 0; j < n; j++) {
                    var x = -halfWidth + j * dx;
                    ret.Vertices.Add(new Vertex(new Vector3(x, 0, z), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector2(j * du, i * dv)));
                }
            }

            for (var i = 0; i < m - 1; i++) {
                for (var j = 0; j < n - 1; j++) {
                    ret.Indices.Add((ushort)(i * n + j));
                    ret.Indices.Add((ushort)(i * n + j + 1));
                    ret.Indices.Add((ushort)((i + 1) * n + j));

                    ret.Indices.Add((ushort)((i + 1) * n + j));
                    ret.Indices.Add((ushort)(i * n + j + 1));
                    ret.Indices.Add((ushort)((i + 1) * n + j + 1));
                }
            }

            return ret;
        }

        public static MeshData CreateFullScreenQuad() {
            var ret = new MeshData();

            ret.Vertices.Add(new Vertex(-1, -1, 0, 0, 0, -1, 1, 0, 0, 0, 1));
            ret.Vertices.Add(new Vertex(-1, 1, 0, 0, 0, -1, 1, 0, 0, 0, 0));
            ret.Vertices.Add(new Vertex(1, 1, 0, 0, 0, -1, 1, 0, 0, 1, 0));
            ret.Vertices.Add(new Vertex(1, -1, 0, 0, 0, -1, 1, 0, 0, 1, 1));

            ret.Indices.AddRange(new ushort[] { 0, 1, 2, 0, 2, 3 });

            return ret;
        }
    }
}