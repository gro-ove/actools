using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Processes {
    public interface IAcsStarter {
        bool RunSteamIfNeeded { get; set; }

        void Run();

        Task RunAsync(CancellationToken cancellation);

        void WaitUntilGame();

        [ItemCanBeNull]
        Task<Process> WaitUntilGameAsync(CancellationToken cancellation);

        void WaitGame();

        Task WaitGameAsync(CancellationToken cancellation);

        void CleanUp();

        Task CleanUpAsync(CancellationToken cancellation);
    }
}
