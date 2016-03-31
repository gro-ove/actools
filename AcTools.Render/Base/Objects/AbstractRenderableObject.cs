using AcTools.Render.Base.Camera;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public abstract class AbstractRenderableObject : IRenderableObject {
        private bool _initialized;

        public Matrix ParentMatrix { get; set; }

        public void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_initialized) {
                Initialize(contextHolder);
                _initialized = true;
            }

            DrawInner(contextHolder, camera, mode);
        }

        protected abstract void Initialize(DeviceContextHolder contextHolder);

        protected abstract void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode);

        public abstract void Dispose();
    }
}
