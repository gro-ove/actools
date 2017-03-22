using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleSky : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleSky([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechSky;
        }

        protected override EffectTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }
    }
}