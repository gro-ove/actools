using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.DeferredShading.Lights {
    public class DirectionalLight : ILight {
        private Vector3 _direction;

        public Vector3 Direction {
            get { return _direction; }
            set { _direction = Vector3.Normalize(value); }
        }

        public Vector3 Color;

        public void Draw(DeviceContext deviceContext, EffectDeferredLight lighting, SpecialLightMode mode) {
            lighting.FxDirectionalLightDirection.Set(Direction);
            lighting.FxLightColor.Set(Color);

            (mode == SpecialLightMode.Default ? lighting.TechDirectionalLight  :
                    mode == SpecialLightMode.Shadows ? lighting.TechDirectionalLight_Shadows :
                    mode == SpecialLightMode.ShadowsWithoutFilter ? lighting.TechDirectionalLight_Shadows_NoFilter :
                            lighting.TechDirectionalLight_Split).DrawAllPasses(deviceContext, 6);
        }
    }
}