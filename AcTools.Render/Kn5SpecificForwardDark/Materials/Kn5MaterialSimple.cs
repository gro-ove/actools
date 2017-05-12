using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimple : Kn5MaterialSimpleBase, IEmissiveMaterial {
        /// <summary>
        /// Should be set before Kn5MaterialSimple.Initialize()
        /// </summary>
        protected uint Flags;

        private EffectDarkMaterial.StandartMaterial _material;
        private IRenderableTexture _txDiffuse;

        internal Kn5MaterialSimple([NotNull] Kn5MaterialDescription description) : base(description) {}

        public override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            if (Kn5Material.AlphaTested) {
                Flags |= EffectDarkMaterial.AlphaTest;
            }

            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _material = new EffectDarkMaterial.StandartMaterial {
                Ambient = Kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = Kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = Kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = Kn5Material.GetPropertyValueAByName("ksSpecularEXP"),
                Emissive = Kn5Material.GetPropertyValueCByName("ksEmissive"),
                Flags = Flags
            };
        }

        public void SetEmissiveNext(Vector3 value, float multipler) {
            var material = _material;
            material.Emissive = material.Emissive * (1f - multipler) + value * multipler;
            Effect.FxMaterial.Set(material);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline &&
                    mode != SpecialRenderMode.Reflection && mode != SpecialRenderMode.Shadow && mode != SpecialRenderMode.GBuffer) return false;

            Effect.FxMaterial.Set(_material);
            Effect.FxDiffuseMap.SetResource(_txDiffuse);

            PrepareStates(contextHolder, mode);
            return true;
        }
    }
}
