using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleGl : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechGl;
        }

        protected override EffectTechnique GetSslrTechnique() {
            return Effect.TechGPass_Gl;
        }
    }

    public class Kn5MaterialSimpleSkinnedGl : Kn5MaterialSimpleGl, ISkinnedMaterial {
        public Kn5MaterialSimpleSkinnedGl([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            InputLayout = Effect.LayoutPNTGW4B;
        }

        protected override EffectTechnique GetShadowTechnique() {
            return Effect.TechSkinnedDepthOnly;
        }

        protected override EffectTechnique GetSslrTechnique() {
            return Effect.TechGPass_SkinnedGl;
        }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechSkinnedGl;
        }

        void ISkinnedMaterial.SetBones(Matrix[] bones) {
            SetBones(bones);
        }
    }
}