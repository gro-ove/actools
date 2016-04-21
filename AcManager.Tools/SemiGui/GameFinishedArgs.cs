using System;
using AcTools.Processes;

namespace AcManager.Tools.SemiGui {
    public class GameFinishedArgs : EventArgs {
        public readonly Game.StartProperties StartProperties;
        public readonly Game.Result Result;

        public GameFinishedArgs(Game.StartProperties startProperties, Game.Result result) {
            StartProperties = startProperties;
            Result = result;
        }
    }
}