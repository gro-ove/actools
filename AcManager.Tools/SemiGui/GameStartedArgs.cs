using System;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class GameStartedArgs : EventArgs {
        [NotNull]
        public readonly Game.StartProperties StartProperties;

        public GameStartedArgs([NotNull] Game.StartProperties startProperties) {
            StartProperties = startProperties;
        }
    }
}