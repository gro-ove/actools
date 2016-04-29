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
    public class Kn5MaterialSimple : IRenderableMaterial, IEmissiveMaterial {
        public bool IsBlending { get; }

        [CanBeNull]
        private readonly string _kn5Filename;

        [CanBeNull]
        private readonly Kn5Material _kn5Material;

        private EffectSimpleMaterial.StandartMaterial _material;
        private EffectSimpleMaterial _effect;

        private IRenderableTexture _txDiffuse, _txNormal, _txMaps, _txDetails,
                _txDetailsNormal;

        internal Kn5MaterialSimple([NotNull] string kn5Filename, [NotNull] Kn5Material material) {
            if (kn5Filename == null) throw new ArgumentNullException(nameof(kn5Filename));
            if (material == null) throw new ArgumentNullException(nameof(material));

            _kn5Filename = kn5Filename;
            _kn5Material = material;

            IsBlending = _kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        protected Kn5MaterialSimple(EffectSimpleMaterial.StandartMaterial material, bool isBlending) {
            _material = material;
            IsBlending = isBlending;
        }

        private IRenderableTexture GetTexture(string mappingName, DeviceContextHolder contextHolder) {
            var mapping = _kn5Material?.GetMappingByName(mappingName);
            return mapping == null || _kn5Filename == null ? null :
                    TexturesProvider.GetTexture(_kn5Filename, mapping.Texture, contextHolder);
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSimpleMaterial>();

            if (_kn5Material == null) return;

            _txDiffuse = GetTexture("txDiffuse", contextHolder);
            _txNormal = _kn5Material.ShaderName.Contains("damage") ? null : GetTexture("txNormal", contextHolder);
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

            if (_kn5Material.GetPropertyValueAByName("useDetail") > 0) {
                flags |= EffectDeferredGObject.HasDetailsMap;
            }

            if (_txDetailsNormal != null) {
                flags |= EffectDeferredGObject.HasDetailsNormalMap;
            }

            if (_kn5Material.ShaderName == "ksTyres" || _kn5Material.ShaderName == "ksBrakeDisc") {
                flags |= EffectDeferredGObject.UseDiffuseAlphaAsMap;
            }

            if (IsBlending) {
                flags |= EffectDeferredGObject.AlphaBlend;
            }

            if (Equals(_kn5Material.GetPropertyValueAByName("isAdditive"), 1.0f)) {
                flags |= EffectDeferredGObject.IsAdditive;
            }

            var specularExp = _kn5Material.GetPropertyValueAByName("ksSpecularEXP");
            if (Equals(_kn5Material.GetPropertyValueAByName("isAdditive"), 2.0f)) {
                specularExp = 250f;
            }

            _material = new EffectSimpleMaterial.StandartMaterial {
                Ambient = _kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = _kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = _kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = specularExp,
                Emissive = _kn5Material.GetPropertyValueCByName("ksEmissive"),
                FresnelC = _kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = _kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = _kn5Material.GetPropertyValueAByName("fresnelMaxLevel"),
                DetailsUvMultipler = _kn5Material.GetPropertyValueAByName("detailUVMultiplier"),
                DetailsNormalBlend = _kn5Material.GetPropertyValueAByName("detailNormalBlend"),
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
            _effect.FxStandartMaterial.Set(material);
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.SimpleTransparent && mode != SpecialRenderMode.Simple) return false;

            _effect.FxStandartMaterial.Set(_material);
            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            _effect.FxNormalMap.SetResource(_txNormal);
            _effect.FxDetailsMap.SetResource(_txDetails);
            _effect.FxDetailsNormalMap.SetResource(_txDetailsNormal);
            _effect.FxMapsMap.SetResource(_txMaps);

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;

            if (IsBlending) {
                contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.TransparentBlendState;
            }

            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechStandard.DrawAllPasses(contextHolder.DeviceContext, indices);

            if (IsBlending) {
                contextHolder.DeviceContext.OutputMerger.BlendState = null;
            }

        }

        public void Dispose() {
        }
    }
}
