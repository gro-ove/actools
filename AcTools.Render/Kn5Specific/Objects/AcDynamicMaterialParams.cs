using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5Specific.Objects {
    public class AcDynamicMaterialParams {
        public SmoothEmissiveChange Emissive { get; } = new SmoothEmissiveChange();

        public float RadialSpeedBlur { get; set; }

        public void SetMaterial(IDeviceContextHolder contextHolder, IAcDynamicMaterial material) {
            if (material == null) return;
            Emissive.SetMaterial(contextHolder, material);

            if (RadialSpeedBlur != 0f) {
                material.SetRadialSpeedBlurNext(RadialSpeedBlur);
            }
        }
    }
}