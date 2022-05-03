using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Base.Materials {
    public class InvisibleMaterial : ISkinnedMaterial {
        public void Dispose() {}

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {}

        public void Refresh(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) => false;

        public void SetMatrices(Matrix objectTransform, ICamera camera) { }

        public void SetBones(Matrix[] bones) { }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {}

        public bool IsBlending => false;

        public string Name => null;
    }
}