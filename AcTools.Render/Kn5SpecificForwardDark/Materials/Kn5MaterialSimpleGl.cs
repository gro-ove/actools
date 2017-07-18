using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleGl : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechGl;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Gl;
        }
    }

    public class Kn5MaterialSimpleSkinnedGl : Kn5MaterialSimpleGl, ISkinnedMaterial {
        public Kn5MaterialSimpleSkinnedGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override void SetInputLayout(IDeviceContextHolder contextHolder) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPNTGW4B;
        }

        protected override EffectReadyTechnique GetShadowTechnique() {
            return Effect.TechSkinnedDepthOnly;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_SkinnedGl;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechSkinnedGl;
        }

        void ISkinnedMaterial.SetBones(Matrix[] bones) {
            SetBones(bones);
        }
    }
}