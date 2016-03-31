using System;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public interface IRenderHelper : IDisposable {
        void Initialize(DeviceContextHolder holder);

        void Resize(DeviceContextHolder holder, int width, int height);

        void Draw(DeviceContextHolder holder, ShaderResourceView view);
    }
}
