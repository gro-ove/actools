using AcTools.Render.Base.Structs;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Render.Base {
    public class QuadBuffers : System.IDisposable {
        private readonly DataStream _verticesStream, _indicesStream;
        private readonly Buffer _verticesBuffer, _indicesBuffer;

        public QuadBuffers(Device device) {
            _verticesStream = new DataStream(new[] {
                new InputLayouts.VerticePT(new Vector3(1, 1, 0.999f), new Vector2(1, 0)),
                new InputLayouts.VerticePT(new Vector3(-1, 1, 0.999f), new Vector2(0, 0)),
                new InputLayouts.VerticePT(new Vector3(-1, -1, 0.999f), new Vector2(0, 1)),
                new InputLayouts.VerticePT(new Vector3(1, -1, 0.999f), new Vector2(1, 1))
            }, false, false);
            _verticesBuffer = new Buffer(device, _verticesStream, new BufferDescription(InputLayouts.VerticePT.StrideValue * 4,
                    ResourceUsage.Immutable, BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0));

            _indicesStream = new DataStream(new ushort[] { 0, 2, 1, 0, 3, 2 }, false, false);
            _indicesBuffer = new Buffer(device, _indicesStream, new BufferDescription(sizeof(short) * 6,
                    ResourceUsage.Immutable, BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0));

            VertexBinding = new VertexBufferBinding(_verticesBuffer, InputLayouts.VerticePT.StrideValue, 0);
        }

        public VertexBufferBinding VertexBinding { get; }

        public void Prepare(DeviceContext deviceContext, InputLayout layout) {
            deviceContext.OutputMerger.DepthStencilState = null;
            PrepareInputAssembler(deviceContext, layout);
        }

        public void PrepareInputAssembler(DeviceContext deviceContext, InputLayout layout) {
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            deviceContext.InputAssembler.InputLayout = layout;
            deviceContext.InputAssembler.SetVertexBuffers(0, VertexBinding);
            deviceContext.InputAssembler.SetIndexBuffer(_indicesBuffer, Format.R16_UInt, 0);
        }

        public void Dispose() {
            _verticesBuffer.Dispose();
            _indicesBuffer.Dispose();
            _verticesStream.Dispose();
            _indicesStream.Dispose();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
    }
}
