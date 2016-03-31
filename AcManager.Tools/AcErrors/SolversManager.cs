using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public interface ISolversFactory {
        ISolver GetSolver(AcObjectNew obj, AcError error);
    }

    public class DefaultFactory : ISolversFactory {
        public ISolver GetSolver([NotNull] AcObjectNew obj, [NotNull] AcError error) {
            switch (error.Type) {
                case AcErrorType.Load_Base:
                    return null;

                case AcErrorType.Data_JsonIsMissing:
                    return new Data_JsonIsMissingSolver((AcJsonObjectNew)obj, error);

                case AcErrorType.Data_JsonIsDamaged:
                    return new Data_JsonIsDamagedSolver((AcJsonObjectNew)obj, error);

                case AcErrorType.Data_CarBrandIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_ObjectNameIsMissing:
                    return new Data_ObjectNameIsMissingSolver((AcCommonObject)obj, error);

                case AcErrorType.CarSkins_DirectoryIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_IniIsMissing:
                    // TODO
                    break;

                case AcErrorType.Data_IniIsDamaged:
                    // TODO
                    break;

                case AcErrorType.Data_UiDirectoryIsMissing:
                    // TODO
                    break;

                case AcErrorType.Car_ParentIsMissing:
                    return new Car_ParentIsMissingSolver((CarObject)obj, error);

                case AcErrorType.Showroom_Kn5IsMissing:
                    return new Showroom_Kn5IsMissingSolver((ShowroomObject)obj, error);

                default:
                    // throw new ArgumentOutOfRangeException();
                    return null;
            }

            return null;
        }
    }

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

        public IEnumerable<Solution> Solutions {
            get { return _solutionsPerSolver.Values.SelectMany(x => x); }
        }
    }

    public static class SolversManager {
        private static readonly List<ISolversFactory> Factories; 

        public static void RegisterFactory(ISolversFactory factory) {
            Factories.Add(factory);
        }

        public static ISolver GetSolver(AcObjectNew acObject, AcError error) {
            if (acObject == null) throw new ArgumentNullException(nameof(acObject));
            if (error == null)  throw new ArgumentNullException(nameof(error));

            var solvers = Factories.Select(x => x.GetSolver(acObject, error)).Where(x => x != null).ToIReadOnlyListIfItsNot();
            return solvers.Count > 1 ? new CombinedSolver(solvers.Reverse()) : solvers.FirstOrDefault();
        }

        static SolversManager() {
            Factories = new List<ISolversFactory>();
            RegisterFactory(new DefaultFactory());   
        }
    }
}
