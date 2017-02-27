using System;
using System.Windows.Forms;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleBase : IRenderableMaterial {
        public bool IsBlending { get; }

        [NotNull]
        protected readonly Kn5MaterialDescription Description;

        // [NotNull]
        // It’s actually not null, but Resharper won’t allow it.
        protected Kn5Material Kn5Material => Description.Material;

        protected EffectDarkMaterial Effect { get; private set; }

        internal Kn5MaterialSimpleBase([NotNull] Kn5MaterialDescription description) {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (description.Material == null) throw new ArgumentNullException(nameof(description.Material));

            Description = description;
            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected IRenderableTexture GetTexture(string mappingName, IDeviceContextHolder contextHolder) {
            var mapping = mappingName == null ? null : Kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, mapping.Texture);
        }

        public virtual void Initialize(IDeviceContextHolder contextHolder) {
            Effect = contextHolder.GetEffect<EffectDarkMaterial>();
            InputLayout = Effect.LayoutPNTG;
        }

        protected InputLayout InputLayout { get; set; }

        protected void PrepareStates(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = InputLayout;

            if (mode == SpecialRenderMode.GBuffer) return;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.States.TransparentBlendState : null;

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
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline &&
                    mode != SpecialRenderMode.Reflection && mode != SpecialRenderMode.Shadow && mode != SpecialRenderMode.GBuffer) return false;
            PrepareStates(contextHolder, mode);
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            Effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            Effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            Effect.FxWorld.SetMatrix(objectTransform);
        }

        protected virtual EffectTechnique GetTechnique() {
            return Effect.TechStandard;
        }

        protected virtual EffectTechnique GetShadowTechnique() {
            return Effect.TechDepthOnly;
        }

        protected virtual EffectTechnique GetSslrTechnique() {
            return Effect.TechGPass_Standard;
        }

        private EffectTechnique GetTechnique(SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Shadow) {
                return GetShadowTechnique();
            }

            if (mode == SpecialRenderMode.GBuffer) {
                Effect.FxGPassTransparent.Set(IsBlending);
                return GetSslrTechnique();
            }

            return GetTechnique();
        }

        public virtual void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            GetTechnique(mode).DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() {}

        #region Bones
        /// <summary>
        /// Call only if material implements ISkinnedMaterial! It would require a different input format.
        /// </summary>
        /// <param name="bones"></param>
        protected void SetBones(Matrix[] bones) {
            if (bones.Length > EffectDarkMaterial.MaxBones) {
                WarnAboutBones(bones.Length);
                Effect.FxBoneTransforms.SetMatrixArray(bones, 0, EffectDarkMaterial.MaxBones);
            } else {
                Effect.FxBoneTransforms.SetMatrixArray(bones);
            }
        }

        private static bool _warnedAboutBones;

        protected static void WarnAboutBones(int count) {
            if (_warnedAboutBones) return;
            _warnedAboutBones = true;
            MessageBox.Show($"Too much bones: {count} (shader limitation: {EffectDarkMaterial.MaxBones})");
        }
        #endregion
    }
}