using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.Lights {
    public interface ILight {
        void Draw(DeviceContext deviceContext, EffectDeferredLight lighting);
    }
}
