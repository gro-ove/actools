using System;
using AcTools.Render.Base.Cameras;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public interface IRenderableObject : IDisposable {
        [CanBeNull]
        string Name { get; }

        Matrix ParentMatrix { get; set; }

        bool IsEnabled { get; set; }

        bool IsReflectable { get; set; }

        int TrianglesCount { get; }

        int ObjectsCount { get; }

        BoundingBox? BoundingBox { get; }

        void Draw(DeviceContextHolder contextHolder, [CanBeNull] ICamera camera, SpecialRenderMode mode, [CanBeNull] Func<IRenderableObject, bool> filter = null);

        void UpdateBoundingBox();

        IRenderableObject Clone();
    }
}
