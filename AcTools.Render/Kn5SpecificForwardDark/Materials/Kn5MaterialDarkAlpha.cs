using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkAlpha : Kn5MaterialDark {
        private EffectDarkMaterial.AlphaMaterial _material;

        public Kn5MaterialDarkAlpha([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            _material = new EffectDarkMaterial.AlphaMaterial {
                Alpha = Kn5Material.GetPropertyValueAByName("alpha")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxAlphaMaterial.Set(_material);
            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechAlpha;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Alpha;
        }
    }
}