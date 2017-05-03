using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleNmMult : Kn5MaterialSimpleNm {
        private EffectDarkMaterial.NmUvMultMaterial _material;

        public Kn5MaterialSimpleNmMult([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
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
            return Effect.TechNmUvMult;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_NmUvMult;
        }
    }
}