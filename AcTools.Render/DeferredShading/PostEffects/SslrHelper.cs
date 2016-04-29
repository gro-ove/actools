using System;
using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.PostEffects {
    public class SslrHelper : IRenderHelper {
        public EffectDeferredPpSslr Effect;

        public void OnInitialize(DeviceContextHolder holder) {
            Effect = holder.GetEffect<EffectDeferredPpSslr>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            throw new NotImplementedException();
        }

        public void Dispose() {}
    }
}
