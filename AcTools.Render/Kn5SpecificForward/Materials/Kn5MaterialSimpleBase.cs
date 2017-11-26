using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class Kn5MaterialSimpleBase : IRenderableMaterial {
        public bool IsBlending { get; }

        [NotNull]
        protected readonly Kn5MaterialDescription Description;

        // [NotNull]
        // It’s actually not null, but Resharper won’t allow it.
        protected Kn5Material Kn5Material => Description.Material;

        protected EffectSimpleMaterial Effect { get; private set; }

        internal Kn5MaterialSimpleBase([NotNull] Kn5MaterialDescription description) {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (description.Material == null) throw new ArgumentNullException(nameof(description.Material));

            Description = description;
            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected IRenderableTexture GetTexture(string mappingName, IDeviceContextHolder contextHolder) {
            var mapping = Kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, mapping.Texture);
        }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (Effect != null) return;
            Effect = contextHolder.GetEffect<EffectSimpleMaterial>();
            Initialize(contextHolder);
        }

        protected virtual void Initialize(IDeviceContextHolder contextHolder) {}

        public void Refresh(IDeviceContextHolder contextHolder) {
            // Because Dispose() is empty, we can just re-initialize shader
            Effect = null;
            EnsureInitialized(contextHolder);
        }

        protected void PrepareStates(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ?
                    contextHolder.States.TransparentBlendState : null;

            if (mode == SpecialRenderMode.SimpleTransparent || mode == SpecialRenderMode.Outline) return;
            switch (Kn5Material.DepthMode) {
                case Kn5MaterialDepthMode.DepthNormal:
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
                    break;

                case Kn5MaterialDepthMode.DepthNoWrite:
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.ReadOnlyDepthState;
                    break;

                case Kn5MaterialDepthMode.DepthOff:
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.DisabledDepthState;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public virtual bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline) return false;
            PrepareStates(contextHolder, mode);
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            Effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            Effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            Effect.FxWorld.SetMatrix(objectTransform);
        }

        public virtual void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            Effect.TechStandard.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() {}
    }
}