using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleAlpha : Kn5MaterialSimple {
        private EffectSimpleMaterial.AlphaMaterial _material;

        public Kn5MaterialSimpleAlpha([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _material = new EffectSimpleMaterial.AlphaMaterial {
                Alpha = Kn5Material.GetPropertyValueAByName("alpha")
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxAlphaMaterial.Set(_material);
            return true;
        }

        public override void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechAlpha.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}