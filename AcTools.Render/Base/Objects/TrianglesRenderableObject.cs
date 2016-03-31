using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Structs;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Objects {
    public class TrianglesRenderableObject<T> : AbstractRenderableObject where T : struct, InputLayouts.ILayout {
        protected bool IsEmpty { get; private set; }

        protected readonly T[] Vertices;
        protected readonly ushort[] Indices;

        private Buffer _verticesBuffer, _indicesBuffer;
        private VertexBufferBinding _verticesBufferBinding;

        public TrianglesRenderableObject(T[] vertices, ushort[] indices) {
            Vertices = vertices;
            Indices = indices;

            IsEmpty = Vertices.Length == 0;
        }

        protected override void Initialize(DeviceContextHolder contextHolder) {
            if (IsEmpty) return;

            var vbd = new BufferDescription(Vertices[0].Stride*Vertices.Length, ResourceUsage.Immutable,
                                            BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            _verticesBuffer = new Buffer(contextHolder.Device, new DataStream(Vertices, false, false), vbd);
            _verticesBufferBinding = new VertexBufferBinding(_verticesBuffer, Vertices[0].Stride, 0);

            var ibd = new BufferDescription(sizeof (ushort)*Indices.Length, ResourceUsage.Immutable,
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
            _verticesBuffer.Dispose();
            _indicesBuffer.Dispose();
        }
    }
}
