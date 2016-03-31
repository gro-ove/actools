using System;
using AcTools.Render.Base.Camera;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public enum SpecialRenderMode {
        Default, Reflection
    }

    public interface IRenderableObject : IDisposable {
        Matrix ParentMatrix { get; set; }

        void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode = SpecialRenderMode.Default);
    }
}
