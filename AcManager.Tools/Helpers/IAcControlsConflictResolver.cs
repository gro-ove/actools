using System.Collections.Generic;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers {
    public interface IAcControlsConflictResolver {
        Task<AcControlsConflictSolution> Resolve(string inputDisplayName, IEnumerable<string> existingAssignments);
    }
}