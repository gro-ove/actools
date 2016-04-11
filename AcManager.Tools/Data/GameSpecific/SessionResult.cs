using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Data.GameSpecific {
    public abstract class SessionResult {
        public abstract string DisplayName { get; }

        public static IEnumerable<SessionResult> GetResults(AcTools.Processes.Game.Result parsedData) {
            return parsedData.Sessions.Select(x => {
                return new CommonSessionResult(x);
            });
        }
    }

    public class CommonSessionResult : SessionResult {
        internal CommonSessionResult(AcTools.Processes.Game.ResultSession parsedData) {
            DisplayName = parsedData.Type.GetDescription() ?? parsedData.Name.ApartFromLast(" Session", StringComparison.OrdinalIgnoreCase);
        }

        public override string DisplayName { get; }
    }

    public class DriftSessionResult : SessionResult {
        public DriftSessionResult() {
        }

        public override string DisplayName => "Drift";
    }
}
