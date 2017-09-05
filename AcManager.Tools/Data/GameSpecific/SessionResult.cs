using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace AcManager.Tools.Data.GameSpecific {
    public abstract class SessionResult {
        public abstract string DisplayName { get; }

        [CanBeNull]
        public static IEnumerable<SessionResult> GetResults(AcTools.Processes.Game.Result parsedData) {
            return parsedData.Sessions?.Select(x => new CommonSessionResult(x));
        }
    }
}
