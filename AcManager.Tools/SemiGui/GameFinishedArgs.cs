using System;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class GameFinishedArgs : EventArgs {
        [NotNull]
        public readonly Game.StartProperties StartProperties;

        [CanBeNull]
        public readonly Game.Result Result;

        public GameFinishedArgs([NotNull] Game.StartProperties startProperties, Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }
}