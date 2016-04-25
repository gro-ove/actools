using AcTools.Render.Base.Cameras;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public abstract class BaseRenderableObject : IRenderableObject {
        private bool _initialized;

        public Matrix ParentMatrix { get; set; }

        public bool IsReflectable { get; set; } = true;

        public BoundingBox? BoundingBox { get; protected set; }

        public void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Reflection && !IsReflectable) return;

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
