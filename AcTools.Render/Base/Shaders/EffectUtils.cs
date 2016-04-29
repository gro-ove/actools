using System.Reflection;
using System.Resources;
using SlimDX;
using SlimDX.D3DCompiler;

namespace AcTools.Render.Base.Shaders {
    public static class EffectUtils {
        private static readonly ResourceManager Shaders = new ResourceManager("AcTools.Render.Shaders", Assembly.GetExecutingAssembly());

        internal static ShaderBytecode Load(string name){
            var bytes = Shaders.GetObject(name) as byte[];
            if (bytes == null) {
                throw new System.Exception("Shader is missing!");
            }

            using (var ds = new DataStream(bytes.Length, true, true)) {
                ds.Write(bytes, 0, bytes.Length);
                return new ShaderBytecode(ds);
            }
        }
    }
}