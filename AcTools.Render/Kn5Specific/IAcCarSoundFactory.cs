using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific {
    public interface IAcCarSoundFactory {
        [ItemCanBeNull]
        Task<IAcCarSound> CreateAsync([NotNull] string carDirectory);
    }

    public interface IAcCarSound : IDisposable {
        void Engine(bool? external, float rpm, float throttle);
        void Turbo(float? value);
        void Limiter(float? value);
        void UpdateEnginePosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity);

        void Door(bool open, TimeSpan delay);
        void UpdateCarPosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity);

        void Horn(bool active);
        void UpdateHornPosition(Vector3 forward, Vector3 position, Vector3 up, Vector3 velocity);
    }
}