using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleAlpha : Kn5MaterialSimple {
        private EffectDarkMaterial.AlphaMaterial _material;

        public Kn5MaterialSimpleAlpha([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
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

    public class Kn5MaterialSimpleWindscreen : Kn5MaterialSimple {
        public Kn5MaterialSimpleWindscreen([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechWindscreen;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }
    }
}