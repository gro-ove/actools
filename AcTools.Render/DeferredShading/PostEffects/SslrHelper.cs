using System;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.PostEffects {
    public class SslrHelper : IRenderHelper {
        public EffectDeferredPpSslr Effect;

        public void Initialize(DeviceContextHolder holder) {
            Effect = holder.GetEffect<EffectDeferredPpSslr>();
        }

        public void Resize(DeviceContextHolder holder, int width, int height) {
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            throw new NotImplementedException();
        }

        public void Dispose() {
        }
    }
}
