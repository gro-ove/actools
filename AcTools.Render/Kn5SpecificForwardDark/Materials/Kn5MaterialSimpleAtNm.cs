using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleAtNm : Kn5MaterialSimple {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleAtNm([NotNull] Kn5MaterialDescription description) : base(description) { }

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
            return Effect.TechAtNm;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_AtNm;
        }
    }
}