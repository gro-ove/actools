using System;
using AcTools.Render.Base;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface ITexturesProvider : IDisposable {
        IRenderableTexture GetTexture(IDeviceContextHolder contextHolder, string key);
    }
}