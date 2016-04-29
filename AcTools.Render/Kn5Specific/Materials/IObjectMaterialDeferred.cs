using SlimDX;

namespace AcTools.Render.Kn5Specific.Materials {
    public interface IEmissiveMaterial {
        void SetEmissive(Vector3 value);

        void SetEmissiveNext(Vector3 value);
    }
}