using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleNmMult : Kn5MaterialSimpleNm {
        private EffectSimpleMaterial.NmUvMultMaterial _material;

        public Kn5MaterialSimpleNmMult([NotNull] Kn5MaterialDescription description) : base(description) { }

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            _material = new EffectSimpleMaterial.NmUvMultMaterial {
                DiffuseMultipler = Kn5Material.GetPropertyValueAByName("diffuseMult"),
                NormalMultipler = Kn5Material.GetPropertyValueAByName("normalMult")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNmUvMultMaterial.Set(_material);
            return true;
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechNmUvMult.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}