using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AcTools.Utils;
using SlimDX;

namespace AcTools.Render.Base.Utils {
    public static class GeometryGenerator {
        private static readonly Vector3[] IcosahedronVertices = {
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

        private static readonly ushort[] IcosahedronIndices = {
            1, 4, 0, 4, 9, 0, 4, 5, 9, 8, 5, 4, 1, 8, 4, 1, 10, 8, 10, 3, 8, 8, 3, 5, 3, 2, 5, 3, 7, 2, 3, 10, 7, 10, 6, 7, 6, 11, 7, 6, 0, 11, 6, 1, 0, 10, 1,
            6, 11, 0, 9, 2, 11, 9, 5, 2, 9, 11, 2, 7
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector3 TangentU;
            public readonly Vector2 TexC;

            public Vertex(Vector3 pos) {
                Position = pos;
                Normal = Vector3.Zero;
                TexC = Vector2.Zero;
                TangentU = Vector3.Zero;
            }

            public Vertex(Vector3 pos, Vector3 norm, Vector2 uv, Vector3 tan) {
                Position = pos;
                Normal = norm;
                TexC = uv;
                TangentU = tan;
            }

            public Vertex(float px, float py, float pz, float nx, float ny, float nz, float u, float v, float tx, float ty, float tz) :
                    this(new Vector3(px, py, pz), new Vector3(nx, ny, nz), new Vector2(u, v), new Vector3(tx, ty, tz)) { }
        }

        public class MeshData {
            public readonly Vertex[] Vertices;
            public readonly ushort[] Indices;

            public MeshData(Vertex[] vertices, ushort[] indices) {
                Vertices = vertices;
                Indices = indices;
            }
        }

        private class MeshBuilder {
            private int _vertexIndex, _indexIndex;
            private bool _fixed;

            public MeshBuilder() {
                VerticesBuffer = new Vertex[40];
                IndicesBuffer = new ushort[60];
                _fixed = false;
            }

            public int VerticesCount => _vertexIndex;
            public int IndicesCount => _indexIndex;

            public MeshBuilder(int verticesCount, int indicesCount) {
                VerticesBuffer = new Vertex[verticesCount];
                IndicesBuffer = new ushort[indicesCount];
                _fixed = true;
            }

            public Vertex[] VerticesBuffer;
            public ushort[] IndicesBuffer;

            private void FitVertices(int count) {
                if (_fixed) {
                    AcToolsLogging.Write($"Wrong vertices estimate: {VerticesBuffer.Length}");
                    _fixed = false;
                }
                Array.Resize(ref VerticesBuffer, (int)(VerticesBuffer.Length * 1.5 + count));
            }

            private void FitIndices(int count) {
                if (_fixed) {
                    AcToolsLogging.Write($"Wrong indices estimate: {IndicesBuffer.Length}");
                    _fixed = false;
                }
                Array.Resize(ref IndicesBuffer, (int)(IndicesBuffer.Length * 1.5 + count));
            }

            public MeshBuilder AppendVertex(Vertex vertex) {
                if (VerticesBuffer.Length == _vertexIndex) {
                    FitVertices(20);
                }
                VerticesBuffer[_vertexIndex++] = vertex;
                return this;
            }

            public MeshBuilder AppendVertex(Vector3 pos, Vector3 norm, Vector2 uv, Vector3 tan) {
                AppendVertex(new Vertex(pos, norm, uv, tan));
                return this;
            }

            public MeshBuilder AppendVertex(float px, float py, float pz, float nx, float ny, float nz, float u, float v, float tx, float ty, float tz) {
                AppendVertex(new Vertex(px, py, pz, nx, ny, nz, u, v, tx, ty, tz));
                return this;
            }

            public MeshBuilder AppendIndex(ushort index) {
                if (IndicesBuffer.Length == _indexIndex) {
                    FitIndices(20);
                    Array.Resize(ref IndicesBuffer, (int)(IndicesBuffer.Length * 1.5 + 20));
                }

                IndicesBuffer[_indexIndex++] = index;
                return this;
            }

            public MeshBuilder AppendIndex(params ushort[] index) {
                if (IndicesBuffer.Length < _indexIndex + index.Length) {
                    FitIndices(index.Length);
                }

                Array.Copy(index, 0, IndicesBuffer, _indexIndex, index.Length);
                _indexIndex += index.Length;
                return this;
            }

            public MeshData Seal() {
                Array.Resize(ref VerticesBuffer, _vertexIndex);
                Array.Resize(ref IndicesBuffer, _indexIndex);
                return new MeshData(VerticesBuffer, IndicesBuffer);
            }
        }

        public static MeshData CreateBox(Vector3 size) {
            return CreateBox(size.X, size.Y, size.Z);
        }

        public static MeshData CreateBox(float width, float height, float depth) {
            var ret = new MeshBuilder(24, 36);

            var w2 = 0.5f * width;
            var h2 = 0.5f * height;
            var d2 = 0.5f * depth;

            // front
            ret.AppendVertex(-w2, -h2, -d2, 0, 0, -1, 0, 1, 1, 0, 0);
            ret.AppendVertex(-w2, +h2, -d2, 0, 0, -1, 0, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 1, 0, 0);
            ret.AppendVertex(+w2, -h2, -d2, 0, 0, -1, 1, 1, 1, 0, 0);

            // back
            ret.AppendVertex(-w2, -h2, +d2, 0, 0, 1, 1, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, +d2, 0, 0, 1, 0, 1, -1, 0, 0);
            ret.AppendVertex(+w2, +h2, +d2, 0, 0, 1, 0, 0, -1, 0, 0);
            ret.AppendVertex(-w2, +h2, +d2, 0, 0, 1, 1, 0, -1, 0, 0);

            // top
            ret.AppendVertex(-w2, +h2, -d2, 0, 1, 0, 0, 1, 1, 0, 0);
            ret.AppendVertex(-w2, +h2, +d2, 0, 1, 0, 0, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, -d2, 0, 1, 0, 1, 1, 1, 0, 0);

            // bottom
            ret.AppendVertex(-w2, -h2, -d2, 0, -1, 0, 1, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, -d2, 0, -1, 0, 0, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, +d2, 0, -1, 0, 0, 0, -1, 0, 0);
            ret.AppendVertex(-w2, -h2, +d2, 0, -1, 0, 1, 0, -1, 0, 0);

            // left
            ret.AppendVertex(-w2, -h2, +d2, -1, 0, 0, 0, 1, 0, 0, -1);
            ret.AppendVertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, 0, 0, -1);
            ret.AppendVertex(-w2, +h2, -d2, -1, 0, 0, 1, 0, 0, 0, -1);
            ret.AppendVertex(-w2, -h2, -d2, -1, 0, 0, 1, 1, 0, 0, -1);

            // right
            ret.AppendVertex(+w2, -h2, -d2, 1, 0, 0, 0, 1, 0, 0, 1);
            ret.AppendVertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 0, 0, 1);
            ret.AppendVertex(+w2, +h2, +d2, 1, 0, 0, 1, 0, 0, 0, 1);
            ret.AppendVertex(+w2, -h2, +d2, 1, 0, 0, 1, 1, 0, 0, 1);

            ret.AppendIndex(0, 1, 2, 0, 2, 3,
                    4, 5, 6, 4, 6, 7,
                    8, 9, 10, 8, 10, 11,
                    12, 13, 14, 12, 14, 15,
                    16, 17, 18, 16, 18, 19,
                    20, 21, 22, 20, 22, 23);
            return ret.Seal();
        }

        public static MeshData CreateLinesBox(Vector3 size) {
            return CreateLinesBox(size.X, size.Y, size.Z);
        }

        public static MeshData CreateLinesBox(float width, float height, float depth) {
            var ret = new MeshBuilder(24, 48);

            var w2 = 0.5f * width;
            var h2 = 0.5f * height;
            var d2 = 0.5f * depth;

            // front
            ret.AppendVertex(-w2, -h2, -d2, 0, 0, -1, 0, 1, 1, 0, 0);
            ret.AppendVertex(-w2, +h2, -d2, 0, 0, -1, 0, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, -d2, 0, 0, -1, 1, 0, 1, 0, 0);
            ret.AppendVertex(+w2, -h2, -d2, 0, 0, -1, 1, 1, 1, 0, 0);

            // back
            ret.AppendVertex(-w2, -h2, +d2, 0, 0, 1, 1, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, +d2, 0, 0, 1, 0, 1, -1, 0, 0);
            ret.AppendVertex(+w2, +h2, +d2, 0, 0, 1, 0, 0, -1, 0, 0);
            ret.AppendVertex(-w2, +h2, +d2, 0, 0, 1, 1, 0, -1, 0, 0);

            // top
            ret.AppendVertex(-w2, +h2, -d2, 0, 1, 0, 0, 1, 1, 0, 0);
            ret.AppendVertex(-w2, +h2, +d2, 0, 1, 0, 0, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, +d2, 0, 1, 0, 1, 0, 1, 0, 0);
            ret.AppendVertex(+w2, +h2, -d2, 0, 1, 0, 1, 1, 1, 0, 0);

            // bottom
            ret.AppendVertex(-w2, -h2, -d2, 0, -1, 0, 1, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, -d2, 0, -1, 0, 0, 1, -1, 0, 0);
            ret.AppendVertex(+w2, -h2, +d2, 0, -1, 0, 0, 0, -1, 0, 0);
            ret.AppendVertex(-w2, -h2, +d2, 0, -1, 0, 1, 0, -1, 0, 0);

            // left
            ret.AppendVertex(-w2, -h2, +d2, -1, 0, 0, 0, 1, 0, 0, -1);
            ret.AppendVertex(-w2, +h2, +d2, -1, 0, 0, 0, 0, 0, 0, -1);
            ret.AppendVertex(-w2, +h2, -d2, -1, 0, 0, 1, 0, 0, 0, -1);
            ret.AppendVertex(-w2, -h2, -d2, -1, 0, 0, 1, 1, 0, 0, -1);

            // right
            ret.AppendVertex(+w2, -h2, -d2, 1, 0, 0, 0, 1, 0, 0, 1);
            ret.AppendVertex(+w2, +h2, -d2, 1, 0, 0, 0, 0, 0, 0, 1);
            ret.AppendVertex(+w2, +h2, +d2, 1, 0, 0, 1, 0, 0, 0, 1);
            ret.AppendVertex(+w2, -h2, +d2, 1, 0, 0, 1, 1, 0, 0, 1);

            ret.AppendIndex(0, 1, 1, 2, 2, 3, 3, 0,
                    4, 5, 5, 6, 6, 7, 7, 4,
                    8, 9, 9, 10, 10, 11, 11, 8,
                    12, 13, 13, 14, 14, 15, 15, 12,
                    16, 17, 17, 18, 18, 19, 19, 16,
                    20, 21, 21, 22, 22, 23, 23, 20);

            return ret.Seal();
        }

        public static MeshData CreateSphere(float radius, int sliceCount, int stackCount) {
            var ret = new MeshBuilder();
            ret.AppendVertex(0, radius, 0, 0, 1, 0, 1, 0, 0, 0, 0);

            var phiStep = MathF.PI / stackCount;
            var thetaStep = 2.0f * MathF.PI / sliceCount;
            for (var i = 1; i <= stackCount - 1; i++) {
                var phi = i * phiStep;
                for (var j = 0; j <= sliceCount; j++) {
                    var theta = j * thetaStep;
                    var p = new Vector3(
                            radius * phi.Sin() * theta.Cos(),
                            radius * phi.Cos(),
                            radius * phi.Sin() * theta.Sin());

                    var t = new Vector3(-radius * phi.Sin() * theta.Sin(), 0, radius * phi.Sin() * theta.Cos());
                    t.Normalize();

                    var n = Vector3.Normalize(p);
                    var uv = new Vector2(theta / (MathF.PI * 2), phi / MathF.PI);
                    ret.AppendVertex(p, n, uv, t);
                }
            }

            ret.AppendVertex(0, -radius, 0, 0, -1, 0, 1, 0, 0, 0, 1);

            for (var i = 1; i <= sliceCount; i++) {
                ret.AppendIndex(0)
                   .AppendIndex((ushort)(i + 1))
                   .AppendIndex((ushort)i);
            }

            var baseIndex = 1;
            var ringVertexCount = sliceCount + 1;
            for (var i = 0; i < stackCount - 2; i++) {
                for (var j = 0; j < sliceCount; j++) {
                    ret.AppendIndex((ushort)(baseIndex + i * ringVertexCount + j))
                       .AppendIndex((ushort)(baseIndex + i * ringVertexCount + j + 1))
                       .AppendIndex((ushort)(baseIndex + (i + 1) * ringVertexCount + j))
                       .AppendIndex((ushort)(baseIndex + (i + 1) * ringVertexCount + j))
                       .AppendIndex((ushort)(baseIndex + i * ringVertexCount + j + 1))
                       .AppendIndex((ushort)(baseIndex + (i + 1) * ringVertexCount + j + 1));
                }
            }

            var southPoleIndex = ret.VerticesCount - 1;
            baseIndex = southPoleIndex - ringVertexCount;
            for (var i = 0; i < sliceCount; i++) {
                ret.AppendIndex((ushort)southPoleIndex)
                   .AppendIndex((ushort)(baseIndex + i))
                   .AppendIndex((ushort)(baseIndex + i + 1));
            }

            return ret.Seal();
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
            var tempMesh = new MeshBuilder {
                VerticesBuffer = IcosahedronVertices.Select(p => new Vertex(p)).ToArray(),
                IndicesBuffer = IcosahedronIndices
            };

            for (var i = 0; i < (int)numSubdivisions; i++) {
                Subdivide4(tempMesh);
            }

            // Project vertices onto sphere and scale.
            for (var i = 0; i < tempMesh.VerticesCount; i++) {
                // Project onto unit sphere.
                var n = Vector3.Normalize(tempMesh.VerticesBuffer[i].Position);

                // Project onto sphere.
                var p = radius * n;

                // Derive texture coordinates from spherical coordinates.
                var theta = MathF.AngleFromXY(tempMesh.VerticesBuffer[i].Position.X, tempMesh.VerticesBuffer[i].Position.Z);
                var phi = (tempMesh.VerticesBuffer[i].Position.Y / radius).Acos();
                var texC = new Vector2(theta / (2 * MathF.PI), phi / MathF.PI);

                // Partial derivative of P with respect to theta
                var tangent = new Vector3(
                        -radius * phi.Sin() * theta.Sin(),
                        0,
                        radius * phi.Sin() * theta.Cos());
                tangent.Normalize();

                tempMesh.VerticesBuffer[i] = new Vertex(p, n, texC, tangent);
            }

            return tempMesh.Seal();
        }

        private static void Subdivide4(MeshBuilder mesh) {
            var newVertices = new Dictionary<Tuple<int, int>, ushort>();
            var indices = new List<ushort>();
            var numTris = mesh.IndicesCount / 3;

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

                var i1 = mesh.IndicesBuffer[i * 3];
                var i2 = mesh.IndicesBuffer[i * 3 + 1];
                var i3 = mesh.IndicesBuffer[i * 3 + 2];
                var a = GetNewVertex(i1, i2);
                var b = GetNewVertex(i2, i3);
                var c = GetNewVertex(i3, i1);

                indices.AddRange(new[] {
                    i1, a, c,
                    i2, b, a,
                    i3, c, b,
                    a, b, c
                });
            }

            mesh.IndicesBuffer = indices.ToArray();

            ushort GetNewVertex(int i1, int i2) {
                var t1 = new Tuple<int, int>(i1, i2);
                var t2 = new Tuple<int, int>(i2, i1);

                if (newVertices.ContainsKey(t2)) {
                    return newVertices[t2];
                }
                if (newVertices.ContainsKey(t1)) {
                    return newVertices[t1];
                }
                var newIndex = (ushort)mesh.VerticesCount;
                newVertices.Add(t1, newIndex);
                mesh.AppendVertex(mesh.VerticesBuffer[i1].Position + mesh.VerticesBuffer[i2].Position,
                        Vector3.Zero, Vector2.Zero, Vector3.Zero);
                return newIndex;
            }
        }

        public static MeshData CreateCylinder(float bottomRadius, float topRadius, float height, int sliceCount, int stackCount, bool caps) {
            if (sliceCount <= 0) throw new ArgumentOutOfRangeException(nameof(sliceCount));

            var ret = new MeshBuilder();

            var stackHeight = height / stackCount;
            var radiusStep = (topRadius - bottomRadius) / stackCount;
            var ringCount = stackCount + 1;

            for (var i = 0; i < ringCount; i++) {
                var y = -0.5f * height + i * stackHeight;
                var r = bottomRadius + i * radiusStep;
                var dTheta = 2.0f * MathF.PI / sliceCount;
                for (var j = 0; j <= sliceCount; j++) {
                    var c = (j * dTheta).Cos();
                    var s = (j * dTheta).Sin();

                    var v = new Vector3(r * c, y, r * s);
                    var uv = new Vector2((float)j / sliceCount, 1.0f - (float)i / stackCount);
                    var t = new Vector3(-s, 0.0f, c);

                    var dr = bottomRadius - topRadius;
                    var bitangent = new Vector3(dr * c, -height, dr * s);

                    var n = Vector3.Normalize(Vector3.Cross(t, bitangent));
                    ret.AppendVertex(v, n, uv, t);
                }
            }
            var ringVertexCount = sliceCount + 1;
            for (var i = 0; i < stackCount; i++) {
                for (var j = 0; j < sliceCount; j++) {
                    ret.AppendIndex((ushort)(i * ringVertexCount + j))
                       .AppendIndex((ushort)((i + 1) * ringVertexCount + j))
                       .AppendIndex((ushort)((i + 1) * ringVertexCount + j + 1))
                       .AppendIndex((ushort)(i * ringVertexCount + j))
                       .AppendIndex((ushort)((i + 1) * ringVertexCount + j + 1))
                       .AppendIndex((ushort)(i * ringVertexCount + j + 1));
                }
            }

            if (caps) {
                BuildCylinderTopCap(topRadius, height, sliceCount, ret);
                BuildCylinderBottomCap(bottomRadius, height, sliceCount, ret);
            }

            return ret.Seal();
        }

        private static void BuildCylinderTopCap(float topRadius, float height, int sliceCount, MeshBuilder ret) {
            var baseIndex = ret.VerticesCount;

            var y = 0.5f * height;
            var dTheta = 2.0f * MathF.PI / sliceCount;

            for (var i = 0; i <= sliceCount; i++) {
                var x = topRadius * (i * dTheta).Cos();
                var z = topRadius * (i * dTheta).Sin();

                var u = x / height + 0.5f;
                var v = z / height + 0.5f;
                ret.AppendVertex(x, y, z, 0, 1, 0, 1, 0, 0, u, v);
            }
            ret.AppendVertex(0, y, 0, 0, 1, 0, 1, 0, 0, 0.5f, 0.5f);
            var centerIndex = ret.VerticesCount - 1;
            for (var i = 0; i < sliceCount; i++) {
                ret.AppendIndex((ushort)centerIndex)
                   .AppendIndex((ushort)(baseIndex + i + 1))
                   .AppendIndex((ushort)(baseIndex + i));
            }
        }

        private static void BuildCylinderBottomCap(float bottomRadius, float height, int sliceCount, MeshBuilder ret) {
            var baseIndex = ret.VerticesCount;

            var y = -0.5f * height;
            var dTheta = 2.0f * MathF.PI / sliceCount;

            for (var i = 0; i <= sliceCount; i++) {
                var x = bottomRadius * (i * dTheta).Cos();
                var z = bottomRadius * (i * dTheta).Sin();

                var u = x / height + 0.5f;
                var v = z / height + 0.5f;
                ret.AppendVertex(x, y, z, 0, -1, 0, 1, 0, 0, u, v);
            }
            ret.AppendVertex(0, y, 0, 0, -1, 0, 1, 0, 0, 0.5f, 0.5f);
            var centerIndex = ret.VerticesCount - 1;
            for (var i = 0; i < sliceCount; i++) {
                ret.AppendIndex((ushort)centerIndex)
                   .AppendIndex((ushort)(baseIndex + i))
                   .AppendIndex((ushort)(baseIndex + i + 1));
            }
        }

        public static MeshData CreateGrid(float width, float depth, int m, int n) {
            var ret = new MeshBuilder();

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
                    ret.AppendVertex(x, 0, z,
                            0, 1, 0,
                            j * du, i * dv, 1, 0, 0);
                }
            }

            for (var i = 0; i < m - 1; i++) {
                for (var j = 0; j < n - 1; j++) {
                    ret.AppendIndex((ushort)(i * n + j))
                       .AppendIndex((ushort)(i * n + j + 1))
                       .AppendIndex((ushort)((i + 1) * n + j))
                       .AppendIndex((ushort)((i + 1) * n + j))
                       .AppendIndex((ushort)(i * n + j + 1))
                       .AppendIndex((ushort)((i + 1) * n + j + 1));
                }
            }

            return ret.Seal();
        }

        public static MeshData CreateFullScreenQuad() {
            var ret = new MeshBuilder(4, 6);
            ret.AppendVertex(-1, -1, 0, 0, 0, -1, 0, 1, 1, 0, 0);
            ret.AppendVertex(-1, 1, 0, 0, 0, -1, 0, 0, 1, 0, 0);
            ret.AppendVertex(1, 1, 0, 0, 0, -1, 1, 0, 1, 0, 0);
            ret.AppendVertex(1, -1, 0, 0, 0, -1, 1, 1, 1, 0, 0);
            ret.AppendIndex(0, 1, 2, 0, 2, 3);
            return ret.Seal();
        }
    }
}