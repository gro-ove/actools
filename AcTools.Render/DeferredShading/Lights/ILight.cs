using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;

namespace AcTools.Render.DeferredShading.Lights {
    public interface ILight : IDisposable {
        void Draw(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode);
    }
}
