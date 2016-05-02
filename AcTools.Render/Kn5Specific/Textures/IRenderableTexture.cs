using System;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface IRenderableTexture : IDisposable {
        [CanBeNull]
        ShaderResourceView Resource { get; }

        [CanBeNull]
        string Name { get; }
    }
}
