using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDark : Kn5MaterialDarkBase, IAcDynamicMaterial {
        /// <summary>
        /// Should be set before Kn5MaterialSimple.Initialize()
        /// </summary>
        protected uint Flags;

        private EffectDarkMaterial.StandartMaterial _material;
        private IRenderableTexture _txDiffuse;

        internal Kn5MaterialDark([NotNull] Kn5MaterialDescription description) : base(description) {}

        protected override void Initialize(IDeviceContextHolder contextHolder) {
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

        protected override EffectDarkMaterial.StandartMaterial CreateWireframeMaterial() {
            var result = base.CreateWireframeMaterial();
            result.Flags |= Flags;
            result.Emissive = _material.Emissive;
            return result;
        }

        protected override void RefreshOverride(IDeviceContextHolder contextHolder) {
            Flags = default(uint);
            base.RefreshOverride(contextHolder);
        }

        private const SpecialRenderMode AllowedFlags = SpecialRenderMode.SimpleTransparent |
                SpecialRenderMode.Simple |
                SpecialRenderMode.Outline |
                SpecialRenderMode.Reflection |
                SpecialRenderMode.Shadow |
                SpecialRenderMode.GBuffer;

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!AllowedFlags.HasFlag(mode)) return false;

            if (Effect?.FxMaterial == null) {
                throw new NullReferenceException(Effect == null ? "Effect is null" : "Effect.FxMaterial is null");
            }

            if (Effect.FxDiffuseMap == null) {
                throw new NullReferenceException("Effect.FxDiffuseMap is null");
            }

            Effect.FxMaterial.Set(_material);
            Effect.FxDiffuseMap.SetResource(_txDiffuse);

            PrepareStates(contextHolder, mode);
            return true;
        }

        void IAcDynamicMaterial.SetEmissiveNext(Vector3 value, float multipler) {
            var material = _material;
            multipler = multipler.Pow((value.Length() / 21f).Clamp(1f, 7f));
            material.Emissive = material.Emissive * (1f - multipler) + value * multipler;
            Effect.FxMaterial.Set(material);
        }

        public virtual void SetRadialSpeedBlurNext(float amount) {}
    }
}
