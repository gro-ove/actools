using System;

namespace AcTools.Render.Base.PostEffects {
    public interface IRenderHelper : IDisposable {
        void OnInitialize(DeviceContextHolder holder);

        void OnResize(DeviceContextHolder holder);
    }
}
