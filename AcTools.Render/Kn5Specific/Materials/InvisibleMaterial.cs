using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public class InvisibleMaterial : IRenderableMaterial {
        public void Dispose() {}

        public void Initialize(DeviceContextHolder contextHolder) {}

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) => false;

        public void SetMatrices(Matrix objectTransform, ICamera camera) {}

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {}

        public bool IsBlending => false;
    }
}