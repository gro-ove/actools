using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public interface IEmissiveMaterial {
        void SetEmissiveNext(Vector3 value, float multipler);
    }
}