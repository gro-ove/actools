using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcErrors.Solver;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public class CombinedSolver : ISolver {
        private readonly Dictionary<ISolver, IReadOnlyList<Solution>> _solutionsPerSolver;

        public CombinedSolver(IEnumerable<ISolver> baseSolvers) {
            _solutionsPerSolver = baseSolvers.ToIReadOnlyListIfItsNot()
                                             .ToDictionary(x => x, x => x.Solutions.ToIReadOnlyListIfItsNot());
        }

        public void OnSuccess(Solution selectedSolution) {
            foreach (var solver in _solutionsPerSolver.Where(x => x.Value.Contains(selectedSolution)).Select(x => x.Key)) {
                solver.OnSuccess(selectedSolution);
            }
        }

        public void OnError(Solution selectedSolution) {
            foreach (var solver in _solutionsPerSolver.Where(x => x.Value.Contains(selectedSolution)).Select(x => x.Key)) {
                solver.OnError(selectedSolution);
            }
        }

        private IReadOnlyList<Solution> _solutions;

        [NotNull]
        public IReadOnlyList<Solution> Solutions => _solutions ?? (_solutions = _solutionsPerSolver.Values.SelectMany(x => x).ToList());
    }
}