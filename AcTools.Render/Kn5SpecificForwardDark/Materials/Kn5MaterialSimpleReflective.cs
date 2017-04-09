using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleReflective : Kn5MaterialSimple {
        private EffectDarkMaterial.ReflectiveMaterial _material;

        public Kn5MaterialSimpleReflective([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected virtual bool IsAdditive() {
            return !Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 0.0f);
        }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            if (IsAdditive()) {
                Flags |= EffectDarkMaterial.IsAdditive;
            }

            _material = new EffectDarkMaterial.ReflectiveMaterial {
                FresnelC = Kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = Kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = Kn5Material.GetPropertyValueAByName("fresnelMaxLevel")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxReflectiveMaterial.Set(_material);
            return true;
        }

        protected override EffectReadyTechnique GetTechnique() {
            return Effect.TechReflective;
        }

        protected override EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Reflective;
        }
    }
}