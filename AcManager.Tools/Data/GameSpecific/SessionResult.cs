using System.Collections.Generic;
using System.Linq;

namespace AcManager.Tools.Data.GameSpecific {
    public abstract class SessionResult {
        public abstract string DisplayName { get; }

        public static IEnumerable<SessionResult> GetResults(AcTools.Processes.Game.Result parsedData) {
            return parsedData.Sessions.Select(x => new CommonSessionResult(x));
        }
    }
}
