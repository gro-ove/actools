using AcTools.Render.Base.Structs;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Render.Base {
    public class QuadBuffers : System.IDisposable {
        public Buffer VertexBuffer, IndexBuffer;

        public QuadBuffers(Device device) {
            VertexBuffer = new Buffer(
                    device, 
                    new DataStream(new[]{
                            new InputLayouts.VerticePT(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                            new InputLayouts.VerticePT(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                            new InputLayouts.VerticePT(new Vector3(1, 1, 0), new Vector2(1, 0)),
                            new InputLayouts.VerticePT(new Vector3(1, -1, 0), new Vector2(1, 1))
                    }, false, false), 
                    new BufferDescription(
                            InputLayouts.VerticePT.StrideValue * 4, 
                            ResourceUsage.Immutable, 
                            BindFlags.VertexBuffer,
                            CpuAccessFlags.None, 
                            ResourceOptionFlags.None, 
                            0
                    )
            );

            IndexBuffer = new Buffer(
                    device, 
                    new DataStream(new ushort[] { 0, 1, 2, 0, 2, 3 }, false, false),
                    new BufferDescription(
                            sizeof(short) * 6, 
                            ResourceUsage.Immutable, 
                            BindFlags.IndexBuffer, 
                            CpuAccessFlags.None, 
                            ResourceOptionFlags.None, 
                            0
                    )
            );

            _vertexBufferBinding = new VertexBufferBinding(VertexBuffer, InputLayouts.VerticePT.StrideValue, 0);
        }

        private readonly VertexBufferBinding _vertexBufferBinding;

        public VertexBufferBinding VertexBinding {
            get { return _vertexBufferBinding; }
        }

        public void Prepare(DeviceContext deviceContext, InputLayout layout) {
            deviceContext.OutputMerger.DepthStencilState = null;
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.InputAssembler.InputLayout = layout;
            deviceContext.InputAssembler.SetVertexBuffers(0, VertexBinding);
            deviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R16_UInt, 0);
        }

        public void Dispose() {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
    }
}
