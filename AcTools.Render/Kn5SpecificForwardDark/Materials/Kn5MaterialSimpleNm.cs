using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleNm : Kn5MaterialSimpleReflective {
        private IRenderableTexture _txNormal;

        public Kn5MaterialSimpleNm([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            if (Kn5Material.GetPropertyValueAByName("nmObjectSpace") != 0) {
                Flags |= EffectDarkMaterial.NmObjectSpace;
            }

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            if (!Effect.FxNormalMap.SetResource(_txNormal)) {
                Effect.FxNormalMap.SetResource(contextHolder.GetFlatNmTexture());
            }

            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechNm;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Nm;
        }
    }
}