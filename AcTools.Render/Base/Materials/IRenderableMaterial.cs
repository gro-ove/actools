using System;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Base.Materials {
    public interface IRenderableMaterial : IDisposable {
        void Initialize(IDeviceContextHolder contextHolder);

        bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode);

        void SetMatrices(Matrix objectTransform, ICamera camera);

        void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode);

        bool IsBlending { get; }
    }
}
