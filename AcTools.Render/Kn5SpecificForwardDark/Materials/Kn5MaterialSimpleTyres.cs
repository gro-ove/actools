using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleTyres : Kn5MaterialSimpleReflective {
        private EffectDarkMaterial.TyresMaterial _material;
        private IRenderableTexture _txNormal, _txDiffuseBlur, _txNormalBlur;

        public Kn5MaterialSimpleTyres([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechTyres;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Tyres;
        }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = GetTexture("txNormal", contextHolder);
            _txDiffuseBlur = GetTexture("txBlur", contextHolder);
            _txNormalBlur = GetTexture("txNormalBlur", contextHolder);

            _material = new EffectDarkMaterial.TyresMaterial {
                BlurLevel = 0f,
                DirtyLevel = 0f
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNormalMap.SetResource(_txNormal);
            Effect.FxDiffuseBlurMap.SetResource(_txDiffuseBlur);
            Effect.FxNormalBlurMap.SetResource(_txNormalBlur);
            Effect.FxTyresMaterial.Set(_material);
            return true;
        }

        public override void SetRadialSpeedBlurNext(float amount) {
            var material = _material;
            material.BlurLevel = amount;
            Effect.FxTyresMaterial.Set(material);
        }
    }
}