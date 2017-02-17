using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Deferred.Kn5Specific.Materials {
    public class Kn5MaterialDeferred : IRenderableMaterial, IEmissiveMaterial {
        public bool IsBlending { get; }

        // [NotNull]
        // Null only for special materials.
        // TODO: Sort this out.
        protected readonly Kn5MaterialDescription Description;

        // [NotNull]
        // It’s actually not null, but Resharper won’t allow it.
        protected Kn5Material Kn5Material => Description.Material;

        private EffectDeferredGObject.Material _material;
        private EffectDeferredGObject _effect;

        private IRenderableTexture _txDiffuse, _txNormal, _txMaps, _txDetails,
                _txDetailsNormal;

        internal Kn5MaterialDeferred([NotNull] Kn5MaterialDescription description) {
            if (description == null) throw new ArgumentNullException(nameof(description));
            if (description.Material == null) throw new ArgumentNullException(nameof(description.Material));

            Description = description;
            IsBlending = Kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected Kn5MaterialDeferred(EffectDeferredGObject.Material material, bool isBlending) {
            _material = material;
            IsBlending = isBlending;
        }

        protected IRenderableTexture GetTexture(string mappingName, IDeviceContextHolder contextHolder) {
            var mapping = Kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, mapping.Texture);
        }

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObject>();

            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _txNormal = Kn5Material.ShaderName.Contains("damage") ? null : GetTexture("txNormal", contextHolder);
            _txMaps = GetTexture("txMaps", contextHolder);
            _txDetails = GetTexture("txDetail", contextHolder);
            _txDetailsNormal = GetTexture("txNormalDetail", contextHolder);

            uint flags = 0;

            if (_txNormal != null) {
                flags |= EffectDeferredGObject.HasNormalMap;
            }

            if (_txMaps != null) {
                flags |= EffectDeferredGObject.HasMaps;
            }

            if (Kn5Material.GetPropertyValueAByName("useDetail") > 0) {
                flags |= EffectDeferredGObject.HasDetailsMap;
            }

            if (_txDetailsNormal != null) {
                flags |= EffectDeferredGObject.HasDetailsNormalMap;
            }

            if (Kn5Material.ShaderName == "ksTyres" || Kn5Material.ShaderName == "ksBrakeDisc") {
                flags |= EffectDeferredGObject.UseDiffuseAlphaAsMap;
            }

            if (IsBlending) {
                flags |= EffectDeferredGObject.AlphaBlend;
            }

            if (Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 1.0f)) {
                flags |= EffectDeferredGObject.IsAdditive;
            }

            var specularExp = Kn5Material.GetPropertyValueAByName("ksSpecularEXP");
            if (Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 2.0f)) {
                specularExp = 250f;
            }

            _material = new EffectDeferredGObject.Material {
                Ambient = Kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = Kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = Kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = specularExp,
                Emissive = Kn5Material.GetPropertyValueCByName("ksEmissive"),
                FresnelC = Kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = Kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = Kn5Material.GetPropertyValueAByName("fresnelMaxLevel"),
                DetailsUvMultipler = Kn5Material.GetPropertyValueAByName("detailUVMultiplier"),
                DetailsNormalBlend = Kn5Material.GetPropertyValueAByName("detailNormalBlend"),
                Flags = flags
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
            _effect.FxMaterial.Set(material);
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.DeferredTransparentMask) return IsBlending;

            if (mode == SpecialRenderMode.Reflection) {
                _effect.FxDiffuseMap.SetResource(_txDiffuse);
                _effect.FxNormalMap.SetResource(_txNormal);
                _effect.FxMaterial.Set(_material);
            } else {
                if ((mode == SpecialRenderMode.DeferredTransparentForw || mode == SpecialRenderMode.DeferredTransparentDepth ||
                        mode == SpecialRenderMode.DeferredTransparentDef) && !IsBlending) return false;

                _effect.FxMaterial.Set(_material);
                _effect.FxDiffuseMap.SetResource(_txDiffuse);
                _effect.FxNormalMap.SetResource(_txNormal);
                _effect.FxDetailsMap.SetResource(_txDetails);
                _effect.FxDetailsNormalMap.SetResource(_txDetailsNormal);
                _effect.FxMapsMap.SetResource(_txMaps);
            }

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public static int Drawed;

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.DeferredTransparentMask || mode == SpecialRenderMode.Shadow) {
                _effect.TechTransparentMask.DrawAllPasses(contextHolder.DeviceContext, indices);
                Drawed++;
                return;
            }

            if (IsBlending) {
                if (mode == SpecialRenderMode.DeferredTransparentForw || mode == SpecialRenderMode.DeferredTransparentDepth) {
                    _effect.TechTransparentForward.DrawAllPasses(contextHolder.DeviceContext, indices);
                    Drawed++;
                } else if (mode == SpecialRenderMode.DeferredTransparentDef) {
                    _effect.TechTransparentDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
                    Drawed++;
                }
                return;
            }

            if (mode == SpecialRenderMode.DeferredTransparentForw || mode == SpecialRenderMode.DeferredTransparentDepth ||
                    mode == SpecialRenderMode.DeferredTransparentDef) return;
            (mode == SpecialRenderMode.Deferred ? _effect.TechStandardDeferred : _effect.TechStandardForward)
                    .DrawAllPasses(contextHolder.DeviceContext, indices);
            Drawed++;
        }

        public void Dispose() {
        }
    }
}
