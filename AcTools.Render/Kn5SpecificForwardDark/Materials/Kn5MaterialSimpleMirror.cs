using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleMirror : Kn5MaterialSimpleBase {
        private EffectDarkMaterial.StandartMaterial _material;
        private EffectDarkMaterial.ReflectiveMaterial _reflMaterial;

        internal Kn5MaterialSimpleMirror() : base(new Kn5MaterialDescription(new Kn5Material())) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _material = new EffectDarkMaterial.StandartMaterial {
                Ambient = 0,
                Diffuse = 0,
                Specular = 1,
                SpecularExp = 1000,
                Emissive = Vector3.Zero,
                Flags = 0
            };

            _reflMaterial = new EffectDarkMaterial.ReflectiveMaterial {
                FresnelC = 1,
                FresnelExp = 1,
                FresnelMaxLevel = 1
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxMaterial.Set(_material);
            Effect.FxReflectiveMaterial.Set(_reflMaterial);
            return true;
        }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechReflective;
        }

        protected override EffectTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Reflective;
        }
    }
}
