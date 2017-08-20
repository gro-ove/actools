using System;
using AcTools.Render.Base;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface IRenderableTexture : IDisposable {
        [CanBeNull]
        ShaderResourceView Resource { get; }

        [CanBeNull]
        string Name { get; }

        bool IsDisposed { get; }
        bool IsOverrideDisabled { get; set; }
        bool Exists { get; }

        void SetProceduralOverride([CanBeNull] IDeviceContextHolder holder, [CanBeNull] ShaderResourceView textureBytes, bool disposeLater);
        void SetProceduralOverride([CanBeNull] IDeviceContextHolder holder, [CanBeNull] byte[] textureBytes);
    }
}
