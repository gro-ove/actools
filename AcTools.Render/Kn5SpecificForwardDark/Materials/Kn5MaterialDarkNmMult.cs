using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkNmMult : Kn5MaterialDarkNm {
        private EffectDarkMaterial.NmUvMultMaterial _material;

        public Kn5MaterialDarkNmMult([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            _material = new EffectDarkMaterial.NmUvMultMaterial {
                DiffuseMultiplier = Kn5Material.GetPropertyValueAByName("diffuseMult"),
                NormalMultiplier = Kn5Material.GetPropertyValueAByName("normalMult")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNmUvMultMaterial.Set(_material);
            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return IsBlending ? Effect.TechNmUvMult : Effect.TechNmUvMult_NoAlpha;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_NmUvMult;
        }
    }
}