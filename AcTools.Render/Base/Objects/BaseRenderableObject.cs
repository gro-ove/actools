using System;
using AcTools.Render.Base.Cameras;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public abstract class BaseRenderableObject : IRenderableObject {
        private bool _initialized;

        public virtual Matrix ParentMatrix { get; set; }

        public bool IsReflectable { get; set; } = true;

        public bool IsEnabled { get; set; } = true;

        public abstract int TrianglesCount { get; }

        public int ObjectsCount => 1;

        public BoundingBox? BoundingBox { get; protected set; }

        public void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!IsEnabled || mode == SpecialRenderMode.Reflection && !IsReflectable) return;

            if (!_initialized) {
                Initialize(contextHolder);
                _initialized = true;
            }

            DrawInner(contextHolder, camera, mode);
        }

        public void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter) {
            if (!filter(this)) return;
            Draw(contextHolder, camera, mode);
        }

        public abstract void UpdateBoundingBox();

        protected abstract void Initialize(DeviceContextHolder contextHolder);

        protected abstract void DrawInner(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode);

        public abstract void Dispose();
    }
}
