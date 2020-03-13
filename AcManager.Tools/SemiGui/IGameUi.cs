using System;
using System.Threading;
using AcTools.Processes;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public interface IGameUi : IDisposable {
        void Show(Game.StartProperties properties, GameMode mode);
        void OnProgress(string message, AsyncProgressEntry? subProgress = null, Action subCancellationCallback = null);
        void OnProgress(Game.ProgressState progress);
        void OnResult([CanBeNull] Game.Result result, [CanBeNull] ReplayHelper replayHelper);
        void OnError(Exception exception);
        CancellationToken CancellationToken { get; }
    }

    public enum GameMode {
        Race, Replay, Benchmark
    }
}