using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Processes {
    public interface IAcsStarter {
        void Run();

        Task RunAsync(CancellationToken cancellation);

        void WaitUntilGame();

        Task WaitUntilGameAsync(CancellationToken cancellation);

        void WaitGame();

        Task WaitGameAsync(CancellationToken cancellation);

        void CleanUp();

        Task CleanUpAsync(CancellationToken cancellation);
    }
}
