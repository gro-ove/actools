using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class DebugMaterial : ISkinnedMaterial, IAcDynamicMaterial {
        private EffectDarkMaterial _effect;
        private EffectDarkMaterial.StandartMaterial _material;
        private IRenderableTexture _txDiffuse, _txNormal;

        [NotNull]
        protected readonly Kn5MaterialDescription Description;

        // [NotNull]
        // It’s actually not null, but Resharper won’t allow it.
        protected Kn5Material Kn5Material => Description.Material;

        internal DebugMaterial(Kn5MaterialDescription kn5Material) {
            Description = kn5Material;
            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDarkMaterial>();
            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _txNormal = Kn5Material.ShaderName.Contains("damage") ? null : GetTexture("txNormal", contextHolder);

            uint flags = 0;

            if (Kn5Material.AlphaTested) {
                flags |= EffectDarkMaterial.AlphaTest;
            }

            if (_txNormal != null) {
                flags |= EffectDarkMaterial.HasNormalMap;
                if (Kn5Material.GetPropertyValueAByName("nmObjectSpace") != 0) {
                    flags |= EffectDarkMaterial.NmObjectSpace;
                }
            }

            if (Kn5Material.ShaderName.Contains("_AT") || Kn5Material.ShaderName == "ksSkinnedMesh") {
                flags |= EffectDarkMaterial.UseNormalAlphaAsAlpha;
            }

            _material = new EffectDarkMaterial.StandartMaterial {
                Ambient = Kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = Kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = Kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = Kn5Material.GetPropertyValueAByName("ksSpecularEXP"),
                Emissive = Kn5Material.GetPropertyValueCByName("ksEmissive"),
                Flags = flags
            };
        }

        public void Refresh(IDeviceContextHolder contextHolder) {
            // Because Dispose() is empty, we can just re-initialize shader
            Initialize(contextHolder);
        }

        protected IRenderableTexture GetTexture(string mappingName, IDeviceContextHolder contextHolder) {
            var mapping = Kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, mapping.Texture);
        }

        protected void PrepareStates(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
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

        private bool _bonesMode;

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline &&
                    mode != SpecialRenderMode.Reflection && mode != SpecialRenderMode.Shadow && mode != SpecialRenderMode.GBuffer) return false;

            _bonesMode = false;
            PrepareStates(contextHolder, mode);
            _effect.FxMaterial.Set(_material);
            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            _effect.FxNormalMap.SetResource(_txNormal);
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void SetBones(Matrix[] bones) {
            _bonesMode = true;
            _effect.FxBoneTransforms.SetMatrixArray(bones);
        }

        protected virtual EffectReadyTechnique GetTechnique() {
            return _bonesMode ? _effect.TechSkinnedDebug : _effect.TechDebug;
        }

        protected virtual EffectReadyTechnique GetShadowTechnique() {
            return _bonesMode ? _effect.TechSkinnedDepthOnly : _effect.TechDepthOnly;
        }

        protected virtual EffectReadyTechnique GetSslrTechnique() {
            return _bonesMode ? _effect.TechGPass_SkinnedDebug : _effect.TechGPass_Debug;
        }

        private EffectReadyTechnique GetTechnique(SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Shadow) {
                return GetShadowTechnique();
            }

            if (mode == SpecialRenderMode.GBuffer) {
                _effect.FxGPassTransparent.Set(IsBlending);
                _effect.FxGPassAlphaThreshold.Set(Kn5Material.AlphaTested ? 0.5f : IsBlending ? 0.0001f : -1f);
                return GetSslrTechnique();
            }

            return GetTechnique();
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            contextHolder.DeviceContext.InputAssembler.InputLayout = _bonesMode ? _effect.LayoutPNTGW4B : _effect.LayoutPNTG;
            GetTechnique(mode).DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }

        public bool IsBlending { get; }

        public void Dispose() { }

        void IAcDynamicMaterial.SetEmissiveNext(Vector3 value, float multipler) {
            var material = _material;
            material.Emissive = material.Emissive * (1f - multipler) + value * multipler;
            _effect.FxMaterial.Set(material);
        }

        void IAcDynamicMaterial.SetRadialSpeedBlurNext(float amount) { }
    }
}