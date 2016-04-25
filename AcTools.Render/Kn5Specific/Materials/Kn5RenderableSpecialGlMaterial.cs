using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public class Kn5RenderableSpecialGlMaterial : IRenderableMaterial {
        private EffectDeferredGObjectSpecial _effect;

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObjectSpecial>();
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            switch (mode) {
                case SpecialRenderMode.Deferred:
                case SpecialRenderMode.Reflection:
                case SpecialRenderMode.Shadow:
                    contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
                    return true;

                default:
                    return false;
            }
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform*camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            switch (mode) {
                case SpecialRenderMode.Deferred:
                    _effect.TechSpecialGlDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;

                case SpecialRenderMode.Reflection:
                    _effect.TechSpecialGlForward.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;

                case SpecialRenderMode.Shadow:
                    _effect.TechSpecialGlMask.DrawAllPasses(contextHolder.DeviceContext, indices);
                    break;
            }
        }

        public void Dispose() {
        }
    }
}
