using AcTools.Processes;

namespace AcManager.Tools.Data.GameSpecific {
    public class CommonSessionResult : SessionResult {
        internal CommonSessionResult(Game.ResultSession parsedData) {
            DisplayName = parsedData.GetSessionName();
        }

        public override string DisplayName { get; }
    }
}