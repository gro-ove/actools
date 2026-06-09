using System;
using System.Collections.Generic;
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

        int GetTrianglesCount();

        int GetObjectsCount();

        [NotNull]
        IEnumerable<int> GetMaterialIds();

        BoundingBox? BoundingBox { get; }

        void Draw(IDeviceContextHolder holder, [CanBeNull] ICamera camera, SpecialRenderMode mode, [CanBeNull] Func<IRenderableObject, bool> filter = null);

        void UpdateBoundingBox();

        [NotNull]
        IRenderableObject Clone();

        float? CheckIntersection(Ray ray);
    }

    public class InvisibleObject : IRenderableObject {
        public void Dispose() {}

        public string Name => null;

        public Matrix ParentMatrix { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsReflectable { get; set; }

        public int GetTrianglesCount() {
            return 0;
        }

        public int GetObjectsCount() {
            return 0;
        }

        public IEnumerable<int> GetMaterialIds() {
            return new int[0];
        }

        public BoundingBox? BoundingBox => null;

        public void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {}

        public void UpdateBoundingBox() {}

        public IRenderableObject Clone() {
            return new InvisibleObject();
        }

        public float? CheckIntersection(Ray ray) {
            return null;
        }
    }
}
