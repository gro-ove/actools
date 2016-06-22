using System.Collections.Generic;

namespace AcManager.Tools.AcErrors {
    public interface ISolver {
        void OnSuccess(Solution selectedSolution);

        void OnError(Solution selectedSolution);

        IReadOnlyList<Solution> Solutions { get; }
    }
}