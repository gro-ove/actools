using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
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
            return property == null ? defaultValue : property.ValueA;
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name, Vector3 defaultValue) {
            var property = mat.GetPropertyByName(name);
            return property == null ? defaultValue : property.ValueC.ToVector3();
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name) {
            return GetPropertyValueCByName(mat, name, Vector3.Zero);
        }

        public static void SetResource(this EffectResourceVariable variable, IRenderableTexture texture) {
            variable.SetResource(texture == null ? null : texture.Resource);
        }
    }

    public class Kn5RenderableMaterial : IRenderableMaterial {
        private readonly string _kn5Filename;
        private readonly Kn5Material _kn5Material;
        private EffectDeferredGObject.Material _material;
        private EffectDeferredGObject _effect;

        private IRenderableTexture _txDiffuse, _txNormal, _txMaps, _txDetails, 
            _txDetailsNormal;

        internal Kn5RenderableMaterial(string kn5Filename, Kn5Material material) {
            _kn5Filename = kn5Filename;
            _kn5Material = material;
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

        public void Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode == SpecialRenderMode.Default) {
                _effect.FxNormalMap.SetResource(_txNormal);
                _effect.FxDetailsNormalMap.SetResource(_txDetailsNormal);
            }
            
            _effect.FxMaterial.Set(_material);
            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            _effect.FxDetailsMap.SetResource(_txDetails);
            _effect.FxMapsMap.SetResource(_txMaps);
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform*camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (_kn5Material.BlendMode == Kn5MaterialBlendMode.AlphaBlend) return;

            (mode == SpecialRenderMode.Default ? _effect.TechStandardDeferred : _effect.TechStandardForward)
                .DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public void Dispose() {
        }
    }
}
