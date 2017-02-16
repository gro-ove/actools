using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
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

        public override void UpdateBoundingBox() {
            // No, we can’t just “cache BB in default state and then transform min/max values”! No!
            // When rotated, some absolutely different vertices might become min/max!

            if (IsEmpty) {
                BoundingBox = null;
            }

            var parentMatrix = ParentMatrix;
            if (parentMatrix == _parentMatrix) return;
            _parentMatrix = parentMatrix;

            var v = Vector3.TransformCoordinate(Vertices[0].Position, parentMatrix);
            var minX = v.X;
            var minY = v.Y;
            var minZ = v.Z;
            var maxX = v.X;
            var maxY = v.Y;
            var maxZ = v.Z;

            for (var i = 1; i < Vertices.Length; i++) {
                var n = Vector3.TransformCoordinate(Vertices[i].Position, parentMatrix);
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
