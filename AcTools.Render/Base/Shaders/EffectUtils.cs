using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using SlimDX;
using SlimDX.D3DCompiler;

namespace AcTools.Render.Base.Shaders {
    public static class EffectUtils {
        public static ShaderBytecode Load(ResourceManager manager, string name) {
            var bytes = manager.GetObject(name) as byte[];
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