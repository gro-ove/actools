using System.ComponentModel;
using AcTools.Processes;

namespace AcManager.Tools.SemiGui {
    public class GameEndedArgs : CancelEventArgs {
        public readonly Game.StartProperties StartProperties;
        public readonly Game.Result Result;

        public GameEndedArgs(Game.StartProperties startProperties, Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }
}