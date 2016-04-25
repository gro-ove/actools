using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Textures;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Materials {
    public static class Kn5MaterialExtend {
        public static float GetPropertyValueAByName(this Kn5Material mat, string name, float defaultValue = 0.0f) {
            var property = mat.GetPropertyByName(name);
            return property?.ValueA ?? defaultValue;
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name, Vector3 defaultValue) {
            var property = mat.GetPropertyByName(name);
            return property?.ValueC.ToVector3() ?? defaultValue;
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name) {
            return GetPropertyValueCByName(mat, name, Vector3.Zero);
        }

        public static void SetResource(this EffectResourceVariable variable, IRenderableTexture texture) {
            variable.SetResource(texture?.Resource);
        }
    }

    public class Kn5RenderableMaterial : IRenderableMaterial {
        public readonly bool IsBlending;

        private readonly string _kn5Filename;
        private readonly Kn5Material _kn5Material;
        private EffectDeferredGObject.Material _material;
        private EffectDeferredGObject _effect;

        private IRenderableTexture _txDiffuse, _txNormal, _txMaps, _txDetails, 
            _txDetailsNormal;

        internal Kn5RenderableMaterial(string kn5Filename, Kn5Material material) {
            _kn5Filename = kn5Filename;
            _kn5Material = material;

            IsBlending = _kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
        }

        private IRenderableTexture GetTexture(string mappingName, DeviceContextHolder contextHolder) {
            var mapping = _kn5Material.GetMappingByName(mappingName);
            return mapping == null ? null : TexturesProvider.GetTexture(_kn5Filename, mapping.Texture, contextHolder);
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObject>();

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

            if (Math.Abs(_kn5Material.GetPropertyValueAByName("isAdditive") - 2.0f) < 0.01f) {
                flags |= EffectDeferredGObject.SpecialMapsMode;
            }

            _material = new EffectDeferredGObject.Material {
                Ambient = _kn5Material.GetPropertyValueAByName("ksAmbient"),
                Diffuse = _kn5Material.GetPropertyValueAByName("ksDiffuse"),
                Specular = _kn5Material.GetPropertyValueAByName("ksSpecular"),
                SpecularExp = _kn5Material.GetPropertyValueAByName("ksSpecularEXP"),
                Emissive = _kn5Material.GetPropertyValueCByName("ksEmissive"),
                FresnelC = _kn5Material.GetPropertyValueAByName("fresnelC"),
                FresnelExp = _kn5Material.GetPropertyValueAByName("fresnelEXP"),
                FresnelMaxLevel = _kn5Material.GetPropertyValueAByName("fresnelMaxLevel"),
                DetailsUvMultipler = _kn5Material.GetPropertyValueAByName("detailUVMultiplier"),
                DetailsNormalBlend = _kn5Material.GetPropertyValueAByName("detailNormalBlend"),
                Flags = flags
            };
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.TransparentMask) return IsBlending;

            if (mode == SpecialRenderMode.Reflection) {
                _effect.FxDiffuseMap.SetResource(_txDiffuse);
                _effect.FxNormalMap.SetResource(_txNormal);
                _effect.FxMaterial.Set(_material);
            } else {
                if ((mode == SpecialRenderMode.Transparent || mode == SpecialRenderMode.TransparentDepth ||
                        mode == SpecialRenderMode.TransparentDeferred) && !IsBlending) return false;

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

        public static int Drawed = 0;

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.TransparentMask || mode == SpecialRenderMode.Shadow) {
                _effect.TechTransparentMask.DrawAllPasses(contextHolder.DeviceContext, indices);
                Drawed++;
                return;
            }

            if (IsBlending) {
                if (mode == SpecialRenderMode.Transparent || mode == SpecialRenderMode.TransparentDepth) {
                    _effect.TechTransparentForward.DrawAllPasses(contextHolder.DeviceContext, indices);
                    Drawed++;
                } else if (mode == SpecialRenderMode.TransparentDeferred) {
                    _effect.TechTransparentDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
                    Drawed++;
                }
                return;
            }

            if (mode == SpecialRenderMode.Transparent || mode == SpecialRenderMode.TransparentDepth ||
                    mode == SpecialRenderMode.TransparentDeferred) return;
            (mode == SpecialRenderMode.Deferred ? _effect.TechStandardDeferred : _effect.TechStandardForward)
                    .DrawAllPasses(contextHolder.DeviceContext, indices);
            Drawed++;
        }

        public void Dispose() {
        }
    }
}
