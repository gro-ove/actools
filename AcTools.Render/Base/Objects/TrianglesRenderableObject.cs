using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Buffer = SlimDX.Direct3D11.Buffer;

namespace AcTools.Render.Base.Objects {
    public class TrianglesRenderableObject<T> : BaseRenderableObject where T : struct, InputLayouts.ILayout {
        protected bool IsEmpty { get; }

        public readonly T[] Vertices;
        public readonly ushort[] Indices;

        public int IndicesCount => Indices.Length;

        private Buffer _verticesBuffer, _indicesBuffer;
        private VertexBufferBinding _verticesBufferBinding;

        public TrianglesRenderableObject(T[] vertices, ushort[] indices) {
            Vertices = vertices;
            Indices = indices;

            IsEmpty = Vertices.Length == 0;
            if (IsEnabled && IsEmpty) {
                IsEnabled = false;
            }
        }

        public IEnumerable<Tuple<Vector3, Vector3, Vector3>> GetTrianglesInWorldSpace() {
            for (var i = 0; i < Indices.Length / 3; i++) {
                var v0 = Vector3.TransformCoordinate(Vertices[Indices[i * 3]].Position, ParentMatrix);
                var v1 = Vector3.TransformCoordinate(Vertices[Indices[i * 3 + 1]].Position, ParentMatrix);
                var v2 = Vector3.TransformCoordinate(Vertices[Indices[i * 3 + 2]].Position, ParentMatrix);
                yield return new Tuple<Vector3, Vector3, Vector3>(v0, v1, v2);
            }
        }

        public override int TrianglesCount => IndicesCount / 3;

        public override void UpdateBoundingBox() {
            BoundingBox = IsEmpty ? (BoundingBox?)null : Vertices.Select(x => Vector3.TransformCoordinate(x.Position, ParentMatrix)).ToBoundingBox();
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            if (IsEmpty) return;

            var vbd = new BufferDescription(Vertices[0].Stride * Vertices.Length, ResourceUsage.Immutable,
                    BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _verticesBuffer = new Buffer(contextHolder.Device, new DataStream(Vertices, false, false), vbd);
            _verticesBufferBinding = new VertexBufferBinding(_verticesBuffer, Vertices[0].Stride, 0);

            var ibd = new BufferDescription(sizeof(ushort) * Indices.Length, ResourceUsage.Immutable,
                    BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _indicesBuffer = new Buffer(contextHolder.Device, new DataStream(Indices, false, false), ibd);
        }

        protected override void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            contextHolder.DeviceContext.InputAssembler.SetVertexBuffers(0, _verticesBufferBinding);
            contextHolder.DeviceContext.InputAssembler.SetIndexBuffer(_indicesBuffer, Format.R16_UInt, 0);
        }

        public override void Dispose() {
            if (IsEmpty) return;
            DisposeHelper.Dispose(ref _verticesBuffer);
            DisposeHelper.Dispose(ref _indicesBuffer);
        }
    }
}
