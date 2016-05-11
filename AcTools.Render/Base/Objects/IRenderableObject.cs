using System;
using AcTools.Render.Base.Cameras;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public interface IRenderableObject : IDisposable {
        Matrix ParentMatrix { get; set; }

        bool IsEnabled { get; set; }

        bool IsReflectable { get; set; }

        int TrianglesCount { get; }

        int ObjectsCount { get; }

        BoundingBox? BoundingBox { get; }

        void Draw(DeviceContextHolder contextHolder, [CanBeNull] ICamera camera, SpecialRenderMode mode);

        void Draw(DeviceContextHolder contextHolder, [CanBeNull] ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter);

        void UpdateBoundingBox();
    }
}
