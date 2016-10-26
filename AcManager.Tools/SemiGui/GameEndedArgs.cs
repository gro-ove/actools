using System.ComponentModel;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class GameEndedArgs : CancelEventArgs {
        [NotNull]
        public readonly Game.StartProperties StartProperties;
        
        [CanBeNull]
        public readonly Game.Result Result;

        public GameEndedArgs([NotNull] Game.StartProperties startProperties, [CanBeNull] Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }
}