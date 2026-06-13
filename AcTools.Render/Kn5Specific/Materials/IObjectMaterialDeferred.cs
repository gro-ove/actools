using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public interface IAcDynamicMaterial {
        void SetEmissiveNext(Vector3 value, float multiplier);

        void SetRadialSpeedBlurNext(float amount);
    }
}