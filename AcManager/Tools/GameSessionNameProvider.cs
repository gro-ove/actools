using System;
using AcManager.Tools.Data.GameSpecific;
using AcTools.Processes;

namespace AcManager.Tools {
    public class GameSessionNameProvider : IGameSessionNameProvider {
        public string GetSessionName(Game.ResultSession parsedData) {
            if (string.Equals(parsedData.Name, "RSR hotlap", StringComparison.OrdinalIgnoreCase) || parsedData.Name == AppStrings.Rsr_SessionName) {
                return AppStrings.Rsr_SessionName;
            }

            return null;
        }
    }
}