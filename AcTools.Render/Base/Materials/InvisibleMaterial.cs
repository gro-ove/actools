using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Base.Materials {
    public class InvisibleMaterial : IRenderableMaterial {
        public void Dispose() {}

        public void Initialize(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) => false;

        public void SetMatrices(Matrix objectTransform, ICamera camera) {}

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {}

        public bool IsBlending => false;
    }
}