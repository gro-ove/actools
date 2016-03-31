using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors {
    public class UiSolversFactory : ISolversFactory {
        public ISolver GetSolver(AcObjectNew obj, AcError error) {
            switch (error.Type) {
                case AcErrorType.Car_ParentIsMissing:
                    return new Car_ParentIsMissingUiSolver((CarObject)obj, error);
            }

            return null;
        }
    }
}
