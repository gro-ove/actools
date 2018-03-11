using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Shaders;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base {
    public interface IDeviceContextHolder {
        [NotNull]
        Device Device { get; }

        [NotNull]
        DeviceContext DeviceContext { get; }

        void Set<T>([NotNull] T obj) where T : class;

        [NotNull]
        T Get<T>() where T : class;

        [CanBeNull]
        T TryToGet<T>() where T : class;

        T GetEffect<T>() where T : IEffectWrapper, new();

        [NotNull]
        IRenderableMaterial GetMaterial(object key);

        [NotNull]
        CommonStates States { get; }

        RendererStopwatch StartNewStopwatch();

        float TimeFactor { get; set; }

        void RaiseUpdateRequired();

        void RaiseSceneUpdated();

        void RaiseTexturesUpdated();

        [NotNull]
        ShaderResourceView GetRandomTexture(int width, int height);

        [NotNull]
        ShaderResourceView GetFlatNmTexture();

        double LastFrameTime { get; }
    }
}