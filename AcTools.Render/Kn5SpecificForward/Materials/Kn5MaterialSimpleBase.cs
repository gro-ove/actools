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
    public class Kn5MaterialSimpleBase : IRenderableMaterial {
        public bool IsBlending { get; }

        protected readonly string Kn5Filename;
        protected readonly Kn5Material Kn5Material;

        protected EffectSimpleMaterial Effect { get; private set; }

        internal Kn5MaterialSimpleBase([NotNull] string kn5Filename, [NotNull] Kn5Material material) {
            if (kn5Filename == null) throw new ArgumentNullException(nameof(kn5Filename));
            if (material == null) throw new ArgumentNullException(nameof(material));

            Kn5Filename = kn5Filename;
            Kn5Material = material;

            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected IRenderableTexture GetTexture(string mappingName, DeviceContextHolder contextHolder) {
            var mapping = Kn5Material?.GetMappingByName(mappingName);
            return mapping == null || Kn5Filename == null ? null :
                    contextHolder.Get<TexturesProvider>().GetTexture(Kn5Filename, mapping.Texture, contextHolder);
        }

        public virtual void Initialize(DeviceContextHolder contextHolder) {
            Effect = contextHolder.GetEffect<EffectSimpleMaterial>();
        }

        public virtual bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple) return false;

            contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            Effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            Effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            Effect.FxWorld.SetMatrix(objectTransform);
        }

        public virtual void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechStandard.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() { }
    }
}