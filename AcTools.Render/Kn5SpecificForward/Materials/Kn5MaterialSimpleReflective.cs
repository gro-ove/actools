using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleReflective : Kn5MaterialSimple {
        private EffectSimpleMaterial.ReflectiveMaterial _material;

        public Kn5MaterialSimpleReflective([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) { }

        public override void Initialize(DeviceContextHolder contextHolder) {
            if (Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 1.0f)) {
                Flags |= EffectSimpleMaterial.IsAdditive;
            }

            _material = new EffectSimpleMaterial.ReflectiveMaterial {
                FresnelC = Kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = Kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = Kn5Material.GetPropertyValueAByName("fresnelMaxLevel")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxReflectiveMaterial.Set(_material);
            return true;
        }

        public override void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechReflective.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}