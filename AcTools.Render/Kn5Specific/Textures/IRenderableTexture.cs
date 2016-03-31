using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface IRenderableTexture : IDisposable {
        ShaderResourceView Resource { get; }
    }
}
