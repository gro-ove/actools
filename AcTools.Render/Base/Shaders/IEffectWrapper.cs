using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shaders {
    public interface IEffectWrapper : System.IDisposable {
        void Initialize(Device device);
    }
}