using System;
using System.Windows.Forms;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialDarkBase : IRenderableMaterial {
        public bool IsBlending { get; private set; }

        [NotNull]
        protected readonly Kn5MaterialDescription Description;

        // [NotNull]
        // It’s actually not null, but Resharper won’t allow it.
        protected Kn5Material Kn5Material => Description.Material;

        public string Name => Kn5Material.Name;

        protected EffectDarkMaterial Effect { get; private set; }

        internal Kn5MaterialDarkBase([NotNull] Kn5MaterialDescription description) {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (description.Material == null) throw new ArgumentNullException(nameof(description.Material));

            Description = description;
            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected IRenderableTexture GetTexture(string mappingName, IDeviceContextHolder contextHolder) {
            var mapping = mappingName == null ? null : Kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, mapping.Texture);
        }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (Effect != null) return;
            Effect = contextHolder.GetEffect<EffectDarkMaterial>();
            Initialize(contextHolder);
        }

        protected virtual void Initialize(IDeviceContextHolder contextHolder) {}

        protected virtual void RefreshOverride(IDeviceContextHolder contextHolder) {
            Effect = null;
            EnsureInitialized(contextHolder);
        }

        public void Refresh(IDeviceContextHolder contextHolder) {
            // Because Dispose() is empty, we can just re-initialize shader
            try {
                IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
                RefreshOverride(contextHolder);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }
        }

        protected virtual void SetInputLayout(IDeviceContextHolder contextHolder) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = Effect.LayoutPNTG;
        }

        protected void PrepareStates(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            SetInputLayout(contextHolder);

            if (mode == SpecialRenderMode.GBuffer) return;
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

        private const SpecialRenderMode Mode = SpecialRenderMode.SimpleTransparent | SpecialRenderMode.Simple
                | SpecialRenderMode.Outline | SpecialRenderMode.Reflection | SpecialRenderMode.Shadow | SpecialRenderMode.GBuffer;

        public virtual bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if ((mode & Mode) == 0) return false;
            PrepareStates(contextHolder, mode);
            return true;
        }

        public virtual void SetMatrices(Matrix objectTransform, ICamera camera) {
            Effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            Effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            Effect.FxWorld.SetMatrix(objectTransform);
        }

        protected virtual EffectReadyTechnique GetTechnique() {
            return IsBlending ? Effect.TechStandard : Effect.TechStandard_NoAlpha;
        }

        protected virtual EffectReadyTechnique GetShadowTechnique() {
            return Effect.TechDepthOnly;
        }

        protected virtual EffectReadyTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Standard;
        }

        private DarkMaterialsParams _materialsParams;

        protected DarkMaterialsParams GetParams(IDeviceContextHolder contextHolder) {
            return _materialsParams ?? (_materialsParams = contextHolder.Get<DarkMaterialsParams>());
        }

        private void DrawShadow(IDeviceContextHolder contextHolder, int indices) {
            GetShadowTechnique().DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        private void DrawGBuffer(IDeviceContextHolder contextHolder, int indices) {
            Effect.FxGPassTransparent.Set(IsBlending);
            Effect.FxGPassAlphaThreshold.Set(Kn5Material.AlphaTested ? 0.5f : IsBlending ? 0.01f : -1f);
            if (GetParams(contextHolder).IsMirrored) {
                contextHolder.DeviceContext.Rasterizer.State = contextHolder.States.InvertedState;
                GetGBufferTechnique().DrawAllPasses(contextHolder.DeviceContext, indices);
                contextHolder.DeviceContext.Rasterizer.State = null;
            } else {
                GetGBufferTechnique().DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        protected virtual EffectDarkMaterial.StandartMaterial CreateWireframeMaterial() {
            return new EffectDarkMaterial.StandartMaterial { Flags = EffectDarkMaterial.DebugUseReflAsColor };
        }

        private void DrawMain(IDeviceContextHolder contextHolder, int indices) {
            var tech = GetTechnique();
            var materialParams = GetParams(contextHolder);
            var isMirrored = materialParams.IsMirrored;
            switch (materialParams.WireframeMode) {
                case WireframeMode.Disabled:
                    if (isMirrored) {
                        contextHolder.DeviceContext.Rasterizer.State = contextHolder.States.InvertedState;
                        tech.DrawAllPasses(contextHolder.DeviceContext, indices);
                        contextHolder.DeviceContext.Rasterizer.State = null;
                    } else {
                        tech.DrawAllPasses(contextHolder.DeviceContext, indices);
                    }
                    break;
                case WireframeMode.LinesOnly:
                    contextHolder.DeviceContext.Rasterizer.State = isMirrored
                            ? contextHolder.States.WireframeInvertedState : contextHolder.States.WireframeState;
                    tech.DrawAllPasses(contextHolder.DeviceContext, indices);
                    contextHolder.DeviceContext.Rasterizer.State = null;
                    break;
                case WireframeMode.Filled:
                    contextHolder.DeviceContext.Rasterizer.State = isMirrored ? contextHolder.States.InvertedState : null;
                    tech.DrawAllPasses(contextHolder.DeviceContext, indices);
                    var depth = contextHolder.DeviceContext.OutputMerger.DepthStencilState;
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.LessEqualReadOnlyDepthState;
                    contextHolder.DeviceContext.Rasterizer.State = materialParams.IsMirrored
                            ? contextHolder.States.WireframeInvertedBiasState : contextHolder.States.WireframeBiasState;
                    if (materialParams.IsWireframeColored) {
                        var v = materialParams.WireframeColor.ToVector3() * materialParams.WireframeBrightness;
                        Effect.FxMaterial.Set(CreateWireframeMaterial());
                        Effect.FxReflectiveMaterial.Set(new EffectDarkMaterial.ReflectiveMaterial { FresnelC = v.X, FresnelExp = v.Y, FresnelMaxLevel = v.Z });
                        GetColoredWireframeTechnique().DrawAllPasses(contextHolder.DeviceContext, indices);
                    } else {
                        GetFilledWireframeTechnique().DrawAllPasses(contextHolder.DeviceContext, indices);
                    }
                    contextHolder.DeviceContext.OutputMerger.DepthStencilState = depth;
                    contextHolder.DeviceContext.Rasterizer.State = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual EffectReadyTechnique GetFilledWireframeTechnique() {
            return IsBlending ? Effect.TechStandard : Effect.TechStandard_NoAlpha;
        }

        protected virtual EffectReadyTechnique GetColoredWireframeTechnique() {
            return IsBlending ? Effect.TechDebug : Effect.TechDebug_NoAlpha;
        }

        public virtual void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Shadow) {
                DrawShadow(contextHolder, indices);
            } else if (mode == SpecialRenderMode.GBuffer) {
                DrawGBuffer(contextHolder, indices);
            } else {
                DrawMain(contextHolder, indices);
            }
        }

        public virtual void Dispose() {}

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