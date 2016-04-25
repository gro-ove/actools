using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public class MirrorMaterial : IRenderableMaterial {
        private static readonly EffectDeferredGObject.Material Material = new EffectDeferredGObject.Material {
            Diffuse = 0,
            Ambient = 0,
            FresnelMaxLevel = 1,
            FresnelC = 1,
            FresnelExp = 0,
            SpecularExp = 400,
            Specular = 1
        };

        private EffectDeferredGObject _effect;

        public void Dispose() {
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObject>();
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Deferred && mode != SpecialRenderMode.Reflection && mode != SpecialRenderMode.Shadow) {
                return false;
            }

            _effect.FxMaterial.Set(Material);
            _effect.FxDiffuseMap.SetResource(null);
            _effect.FxNormalMap.SetResource(null);
            _effect.FxDetailsMap.SetResource(null);
            _effect.FxDetailsNormalMap.SetResource(null);
            _effect.FxMapsMap.SetResource(null);
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            switch (mode) {
                case SpecialRenderMode.Deferred:
                    _effect.TechStandardDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;

                case SpecialRenderMode.Reflection:
                    _effect.TechStandardForward.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;

                case SpecialRenderMode.Shadow:
                    _effect.TechTransparentMask.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;
            }
        }
    }
}