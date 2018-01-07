using System;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class GameStartedArgs : EventArgs {
        [NotNull]
        public readonly Game.StartProperties StartProperties;

        public readonly GameMode Mode;

        public GameStartedArgs([NotNull] Game.StartProperties startProperties, GameMode mode) {
            StartProperties = startProperties;
            Mode = mode;
        }
    }
}