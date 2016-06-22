using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public interface ISolversFactory {
        ISolver GetSolver(AcObjectNew obj, AcError error);
    }
}