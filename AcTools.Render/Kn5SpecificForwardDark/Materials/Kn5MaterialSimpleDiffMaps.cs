using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    /*public class Kn5MaterialSimpleDiffMaps : Kn5MaterialSimpleReflective {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleDiffMaps([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechDiffMaps;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_DiffMaps;
        }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNormalMap.SetResource(_txNormal);
            return true;
        }
    }*/
}