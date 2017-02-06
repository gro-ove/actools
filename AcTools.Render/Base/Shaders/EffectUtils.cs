using System.IO;
using System.IO.Compression;
using System.Resources;
using SlimDX;
using SlimDX.D3DCompiler;

namespace AcTools.Render.Base.Shaders {
    public static class EffectUtils {
        private static byte[] Decompress(byte[] data) {
            var output = new MemoryStream();
            using (var dstream = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress)) {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }

        public static ShaderBytecode Load(ResourceManager manager, string name) {
            var bytes = manager.GetObject(name) as byte[];
            if (bytes == null) {
                throw new System.Exception("Shader is missing!");
            }

            bytes = Decompress(bytes);
            using (var ds = new DataStream(bytes.Length, true, true)) {
                ds.Write(bytes, 0, bytes.Length);
                return new ShaderBytecode(ds);
            }
        }
    }
}