using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;

namespace AcTools.Render.DeferredShading.Lights {
    public abstract class BaseLight : ILight {
        private bool _initialized;

        public virtual void Dispose() {}

        public abstract void OnInitialize(DeviceContextHolder holder);

        public void Draw(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode) {
            if (!_initialized) {
                OnInitialize(holder);
                _initialized = true;
            }

            DrawInner(holder, camera, mode);
        }

        public abstract void DrawInner(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode);
    }
}