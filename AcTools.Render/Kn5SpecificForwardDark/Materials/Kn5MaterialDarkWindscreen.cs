using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkWindscreen : Kn5MaterialDark {
        public Kn5MaterialDarkWindscreen([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return IsBlending ? Effect.TechWindscreen : Effect.TechWindscreen_NoAlpha;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }
    }
}