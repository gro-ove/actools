using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Structs;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Buffer = SlimDX.Direct3D11.Buffer;

namespace AcTools.Render.Base.Objects {
    public class TrianglesRenderableObject<T> : BaseRenderableObject where T : struct, InputLayouts.IPositionLayout {
        protected bool IsEmpty { get; private set; }
        
        public T[] Vertices { get; private set; }

        public ushort[] Indices { get; private set; }

        public int IndicesCount { get; private set; }

        private DataStream _verticesStream, _indicesStream;
        private Buffer _verticesBuffer, _indicesBuffer;
        private VertexBufferBinding _verticesBufferBinding;

        public TrianglesRenderableObject([CanBeNull] string name, T[] vertices, ushort[] indices) : base(name) {
            Vertices = vertices;
            Indices = indices;
            IndicesCount = Indices.Length;
            IsEmpty = IndicesCount == 0;
        }

        public override bool IsEnabled {
            get { return !IsEmpty && base.IsEnabled; }
            set { base.IsEnabled = value; }
        }

        public override int GetTrianglesCount() {
            return IndicesCount / 3;
        }

        private Matrix? _parentMatrix;
        private Vector3[] _positions;

        private void Prepare() {
            var positions = new Vector3[Vertices.Length];
            for (var i = 0; i < positions.Length; i++) {
                positions[i] = Vertices[i].Position;
            }
            _positions = positions;
        }

        private void PrepareSmart() {
            var positions = new bool[Vertices.Length];

            var filtered = new List<Vector3>(Vertices.Length);
            for (var i = 0; i < Vertices.Length; i++) {
                var p = Vertices[i].Position;
                if (positions[i]) continue;

                for (int j = i + 1, l = Math.Min(Vertices.Length, i + 10); j < l; j++) {
                    var n = Vertices[j].Position;
                    if (!positions[j] && Vector3.Dot(p, n) > 0.95) {
                        if (p.LengthSquared() > n.LengthSquared()) {
                            positions[j] = true;
                        } else {
                            goto Next;
                        }
                    }
                }

                filtered.Add(p);

                Next:
                continue;
            }

            // MessageBox.Show($"{Name}: {100d * filtered.Count / positions.Length:F1}% ({Vertices.Length})");
            _positions = filtered.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 TransformCoordinate(Vector3 c, Matrix m) {
            var x = m.M11 * c.X + m.M21 * c.Y + m.M31 * c.Z + m.M41;
            var y = m.M12 * c.X + m.M22 * c.Y + m.M32 * c.Z + m.M42;
            var z = m.M13 * c.X + m.M23 * c.Y + m.M33 * c.Z + m.M43;
            var n = 1f / (m.M14 * c.X + m.M24 * c.Y + m.M34 * c.Z + m.M44);
            return new Vector3(x * n, y * n, z * n);
        }

        public override void UpdateBoundingBox() {
            // No, we can’t just “cache BB in default state and then transform min/max values”! No!
            // When rotated, some absolutely different vertices might become min/max!

            if (IsEmpty) {
                BoundingBox = null;
            }

            var parentMatrix = ParentMatrix;
            if (parentMatrix == _parentMatrix) return;
            _parentMatrix = parentMatrix;

            if (_positions == null) {
                PrepareSmart();
                if (_positions == null) return;
            }

            var v = TransformCoordinate(_positions[0], parentMatrix);
            var minX = v.X;
            var minY = v.Y;
            var minZ = v.Z;
            var maxX = v.X;
            var maxY = v.Y;
            var maxZ = v.Z;

            for (var i = 1; i < _positions.Length; i++) {
                var n = TransformCoordinate(_positions[i], parentMatrix);
                if (minX > n.X) minX = n.X;
                if (minY > n.Y) minY = n.Y;
                if (minZ > n.Z) minZ = n.Z;
                if (maxX < n.X) maxX = n.X;
                if (maxY < n.Y) maxY = n.Y;
                if (maxZ < n.Z) maxZ = n.Z;
            }

            BoundingBox = new BoundingBox(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }

        public override BaseRenderableObject Clone() {
            return new TrianglesRenderableObject<T>(Name + "_copy", Vertices, Indices) {
                IsReflectable = IsReflectable,
                IsEnabled = IsEnabled
            };
        }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            if (IsEmpty) return;

            var vbd = new BufferDescription(Vertices[0].Stride * Vertices.Length, ResourceUsage.Immutable,
                    BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _verticesStream = new DataStream(Vertices, false, false);
            _verticesBuffer = new Buffer(contextHolder.Device, _verticesStream, vbd);
            _verticesBufferBinding = new VertexBufferBinding(_verticesBuffer, Vertices[0].Stride, 0);

            var ibd = new BufferDescription(sizeof(ushort) * Indices.Length, ResourceUsage.Immutable,
                    BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _indicesStream = new DataStream(Indices, false, false);
            _indicesBuffer = new Buffer(contextHolder.Device, _indicesStream, ibd);
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            var assembler = contextHolder.DeviceContext.InputAssembler;
            assembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            assembler.SetVertexBuffers(0, _verticesBufferBinding);
            assembler.SetIndexBuffer(_indicesBuffer, Format.R16_UInt, 0);
        }

        /// <summary>
        /// Returns distance.
        /// </summary>
        public override float? CheckIntersection(Ray ray) {
            UpdateBoundingBox();

            float d;
            if (!BoundingBox.HasValue || !Ray.Intersects(ray, BoundingBox.Value, out d)) {
                return null;
            }
            
            var min = float.MaxValue;
            var found = false;

            var indices = Indices;
            var vertices = Vertices;
            var matrix = ParentMatrix;
            for (int i = 0, n = indices.Length / 3; i < n; i++) {
                var v0 = Vector3.TransformCoordinate(vertices[indices[i * 3]].Position, matrix);
                var v1 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 1]].Position, matrix);
                var v2 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 2]].Position, matrix);

                float distance;
                if (!Ray.Intersects(ray, v0, v1, v2, out distance) || distance >= min) continue;
                min = distance;
                found = true;
            }

            return found ? min : (float?)null;
        }

        public override void Dispose() {
            Vertices = new T[0];
            Indices = new ushort[0];
            IndicesCount = 0;
            IsEmpty = true;
            
            DisposeHelper.Dispose(ref _verticesBuffer);
            DisposeHelper.Dispose(ref _indicesBuffer);
            DisposeHelper.Dispose(ref _verticesStream);
            DisposeHelper.Dispose(ref _indicesStream);
        }
    }
}
