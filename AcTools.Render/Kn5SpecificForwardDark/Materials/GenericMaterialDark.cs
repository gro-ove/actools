using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    /// <summary>
    /// For procedurally generated objects.
    /// </summary>
    public class GenericMaterialDark : Kn5MaterialDarkBase, IAcDynamicMaterial {
        public EffectDarkMaterial.StandartMaterial Material;

        [CanBeNull]
        public IRenderableTexture TxDiffuse;

        internal GenericMaterialDark(bool isBlending) : base(new Kn5MaterialDescription(new Kn5Material {
            BlendMode = isBlending ? Kn5MaterialBlendMode.AlphaBlend : Kn5MaterialBlendMode.Opaque
        })) {
            Material = default(EffectDarkMaterial.StandartMaterial);
            TxDiffuse = null;
        }

        protected override EffectDarkMaterial.StandartMaterial CreateWireframeMaterial() {
            var result = base.CreateWireframeMaterial();
            result.Flags |= Material.Flags;
            result.Emissive = Material.Emissive;
            return result;
        }

        private const SpecialRenderMode AllowedFlags = SpecialRenderMode.SimpleTransparent |
                SpecialRenderMode.Simple |
                SpecialRenderMode.Outline |
                SpecialRenderMode.Reflection |
                SpecialRenderMode.Shadow |
                SpecialRenderMode.GBuffer;

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!AllowedFlags.HasFlag(mode)) return false;
            Effect.FxMaterial.Set(Material);
            Effect.FxDiffuseMap.SetResource(TxDiffuse);
            PrepareStates(contextHolder, mode);
            return true;
        }

        void IAcDynamicMaterial.SetEmissiveNext(Vector3 value, float multipler) {
            var material = Material;
            multipler = multipler.Pow((value.Length() / 21f).Clamp(1f, 7f));
            material.Emissive = material.Emissive * (1f - multipler) + value * multipler;
            Effect.FxMaterial.Set(material);
        }

        public void SetRadialSpeedBlurNext(float amount) {}

        public override void Dispose() {
            DisposeHelper.Dispose(ref TxDiffuse);
            base.Dispose();
        }
    }
}