using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkAtNm : Kn5MaterialDark {
        private IRenderableTexture _txNormal;

        public Kn5MaterialDarkAtNm([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNormalMap.SetResource(_txNormal);
            if (Kn5Material.GetPropertyValueAByName("nmObjectSpace") != 0) {
                Flags |= EffectDarkMaterial.NmObjectSpace;
            }

            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return IsBlending ? Effect.TechAtNm : Effect.TechAtNm_NoAlpha;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_AtNm;
        }
    }
}