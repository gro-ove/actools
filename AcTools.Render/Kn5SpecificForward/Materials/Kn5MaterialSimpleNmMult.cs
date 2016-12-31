using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleNmMult : Kn5MaterialSimpleNm {
        private EffectSimpleMaterial.NmUvMultMaterial _material;

        public Kn5MaterialSimpleNmMult([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) { }

        public override void Initialize(DeviceContextHolder contextHolder) {
            _material = new EffectSimpleMaterial.NmUvMultMaterial {
                DiffuseMultipler = Kn5Material.GetPropertyValueAByName("diffuseMult"),
                NormalMultipler = Kn5Material.GetPropertyValueAByName("normalMult")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxNmUvMultMaterial.Set(_material);
            return true;
        }

        public override void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechNmUvMult.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}