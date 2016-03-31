using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.Lights {
    public class PointLight : ILight {
        public Vector3 Position;
        public Vector3 Color;
        public float Radius;

        public void Draw(DeviceContext deviceContext, EffectDeferredLight lighting) {
            lighting.FxPointLightRadius.Set(Radius);
            lighting.FxPointLightPosition.Set(Position);
            lighting.FxLightColor.Set(Color);

            lighting.TechPointLight.DrawAllPasses(deviceContext, 6);
        }
    }
}
