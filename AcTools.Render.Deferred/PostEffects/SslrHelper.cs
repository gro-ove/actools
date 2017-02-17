using System;
using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Shaders;
using SlimDX.Direct3D11;

namespace AcTools.Render.Deferred.PostEffects {
    public class SslrHelper : IRenderHelper {
        public EffectDeferredPpSslr Effect;

        public void OnInitialize(DeviceContextHolder holder) {
            Effect = holder.GetEffect<EffectDeferredPpSslr>();
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            throw new NotSupportedException();
        }

        public void Dispose() {}
    }
}
