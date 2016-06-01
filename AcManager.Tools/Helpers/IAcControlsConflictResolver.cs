using System.Collections.Generic;

namespace AcManager.Tools.Helpers {
    public interface IAcControlsConflictResolver {
        AcControlsConflictSolution Resolve(string inputDisplayName, IEnumerable<string> existingAssignments);
    }
}