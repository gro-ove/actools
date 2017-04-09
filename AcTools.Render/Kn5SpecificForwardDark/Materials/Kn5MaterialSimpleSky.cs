using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleSky : Kn5MaterialSimpleBase {
        public Kn5MaterialSimpleSky([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechSky;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }
    }
}