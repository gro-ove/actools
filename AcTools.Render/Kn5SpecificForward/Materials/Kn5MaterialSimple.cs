using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimple : Kn5MaterialSimpleBase, IEmissiveMaterial {
        /// <summary>
        /// Should be set before Kn5MaterialSimple.Initialize()
        /// </summary>
        protected uint Flags;

        private EffectSimpleMaterial.StandartMaterial _material;
        private IRenderableTexture _txDiffuse;

        internal Kn5MaterialSimple([NotNull] string kn5Filename, [NotNull] Kn5Material material) : base(kn5Filename, material) {}

        public override void Initialize(DeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);

            if (Kn5Material.AlphaTested) {
                Flags |= EffectSimpleMaterial.AlphaTest;
            }

            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _material = new EffectSimpleMaterial.StandartMaterial {
                Ambient = Kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = Kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = Kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = Kn5Material.GetPropertyValueAByName("ksSpecularEXP"),
                Emissive = Kn5Material.GetPropertyValueCByName("ksEmissive"),
                Flags = Flags
            };
        }

        public void SetEmissive(Vector3 value) {
            SetEmissiveNext(value);

            var material = _material;
            material.Emissive = value;
            _material = material;
        }

        public void SetEmissiveNext(Vector3 value) {
            var material = _material;
            material.Emissive = value;
            Effect.FxMaterial.Set(material);
        }

        public override bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple) return false;

            Effect.FxMaterial.Set(_material);
            Effect.FxDiffuseMap.SetResource(_txDiffuse);

            contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.TransparentBlendState : null;

            return true;
        }

        public override void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechStandard.DrawAllPasses(contextHolder.DeviceContext, indices);
        }
    }
}
