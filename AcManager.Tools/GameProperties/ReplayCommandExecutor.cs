using System;
using AcManager.Tools.Helpers;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class ReplayCommandExecutor : GameCommandExecutorBase {
        public ReplayCommandExecutor(Game.StartProperties properties) : base(properties) { }

        public override IDisposable Set() {
            Execute(SettingsHolder.Drive.PreReplayCommand);
            return this;
        }

        public override void Dispose() {
            Execute(SettingsHolder.Drive.PostReplayCommand);
        }
    }
}