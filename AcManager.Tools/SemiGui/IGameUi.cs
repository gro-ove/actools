using System;
using System.Threading;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public interface IGameUi : IDisposable {
        void Show(Game.StartProperties properties, GameMode mode);
        void OnProgress(string message);
        void OnProgress(Game.ProgressState progress);
        void OnResult([CanBeNull] Game.Result result, [CanBeNull] ReplayHelper replayHelper);
        void OnError(Exception exception);
        CancellationToken CancellationToken { get; }
    }

    public enum GameMode {
        Race, Replay, Benchmark
    }
}