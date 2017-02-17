using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Deferred.Kn5Specific.Materials {
    public class Kn5MaterialGlDeferred : IRenderableMaterial {
        private EffectDeferredGObjectSpecial _effect;

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObjectSpecial>();
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
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
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorldInvTranspose.SetMatrix(Matrix.Invert(Matrix.Transpose(objectTransform)));
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
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

        public bool IsBlending => false;

        public void Dispose() {}
    }
}