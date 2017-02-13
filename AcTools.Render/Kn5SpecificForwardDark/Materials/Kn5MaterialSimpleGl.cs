using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleGl : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechGl.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }

    public class Kn5MaterialSimpleSkinnedGl : Kn5MaterialSimpleGl, ISkinnedMaterial {
        public Kn5MaterialSimpleSkinnedGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            InputLayout = Effect.LayoutPNTGW4B;
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechSkinnedGl.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void SetBones(Matrix[] bones) {
            Effect.FxBoneTransforms.SetMatrixArray(bones);
        }
    }
}