using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.DeferredShading.Lights {
    public class DirectionalLight : BaseLight {
        private Vector3 _direction;

        public Vector3 Direction {
            get { return _direction; }
            set { _direction = Vector3.Normalize(value); }
        }

        public Vector3 Color;

        private EffectDeferredLight _effect;

        public override void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectDeferredLight>();
        }

        public void Resize(DeviceContextHolder holder, int width, int height) {}

        public override void DrawInner(DeviceContextHolder holder, ICamera camera, SpecialLightMode mode) {
            _effect.FxDirectionalLightDirection.Set(Direction);
            _effect.FxLightColor.Set(Color);

            // using this instead of .Prepare() to keep DepthState — it might be special for filtering
            holder.QuadBuffers.PrepareInputAssembler(holder.DeviceContext, _effect.LayoutPT);
            
            (mode == SpecialLightMode.Default ? _effect.TechDirectionalLight  :
                    mode == SpecialLightMode.Shadows ? _effect.TechDirectionalLight_Shadows :
                    mode == SpecialLightMode.ShadowsWithoutFilter ? _effect.TechDirectionalLight_Shadows_NoFilter :
                            _effect.TechDirectionalLight_Split).DrawAllPasses(holder.DeviceContext, 6);
        }

        public override void Dispose() {
            _effect = null;
        }
    }
}