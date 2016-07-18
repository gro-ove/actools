using System;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Data.GameSpecific {
    public class CommonSessionResult : SessionResult {
        internal CommonSessionResult(Game.ResultSession parsedData) {
            DisplayName = parsedData.Type.GetDisplayName() ?? parsedData.Name.ApartFromLast(@" Session", StringComparison.OrdinalIgnoreCase);
        }

        public override string DisplayName { get; }
    }
}