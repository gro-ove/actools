using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkSky : Kn5MaterialDarkBase {
        public Kn5MaterialDarkSky([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechSky;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }
    }
}