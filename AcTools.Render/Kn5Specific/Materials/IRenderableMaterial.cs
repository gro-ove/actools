using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public interface IRenderableMaterial : IDisposable {
        void Initialize(DeviceContextHolder contextHolder);

        void Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode);

        void SetMatrices(Matrix objectTransform, ICamera camera);

        void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode);
    }
}
