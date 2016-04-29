using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificDeferred.Materials {
    public class SkyMaterialDeferred : IRenderableMaterial {
        private EffectDeferredGSky _effect;

        public Vector3 SkyColorLower { get; set; } = Vector3.Normalize(new Vector3(35, 83, 167)) * 2.1f;

        public Vector3 SkyColorUpper { get; set; } = Vector3.Normalize(new Vector3(35, 83, 167)) * 2.4f;

        private RasterizerState _rasterizerState;

        public void Dispose() {
            DisposeHelper.Dispose(ref _rasterizerState);
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGSky>();
            _rasterizerState = RasterizerState.FromDescription(contextHolder.Device, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.Front,
                IsAntialiasedLineEnabled = false,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            });
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Deferred && mode != SpecialRenderMode.Reflection) return false;

            _effect.FxSkyDown.Set(SkyColorLower);
            _effect.FxSkyRange.Set(SkyColorUpper - SkyColorLower);
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(Matrix.Translation(camera.Position) * camera.ViewProj);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            contextHolder.DeviceContext.Rasterizer.State = _rasterizerState;

            if (mode == SpecialRenderMode.Deferred) {
                _effect.TechSkyDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
            } else if (mode == SpecialRenderMode.Reflection) { 
                _effect.TechSkyForward.DrawAllPasses(contextHolder.DeviceContext, indices);
            }

            contextHolder.DeviceContext.Rasterizer.State = null;
        }

        public bool IsBlending => false;
    }
}