using System.Runtime.InteropServices;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Kn5Render.Kn5Render {
    public class SquareBuffers : System.IDisposable {
        public Buffer VertexBuffer, IndexBuffer;

        public SquareBuffers(Device device) {
            VertexBuffer = new Buffer(
                    device, 
                    new DataStream(new[]{
                            new VerticePT(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                            new VerticePT(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                            new VerticePT(new Vector3(1, 1, 0), new Vector2(1, 0)),
                            new VerticePT(new Vector3(1, -1, 0), new Vector2(1, 1))
                    }, false, false), 
                    new BufferDescription(
                            VerticePT.Stride * 4, 
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
