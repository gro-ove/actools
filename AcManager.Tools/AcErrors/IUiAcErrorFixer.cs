using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public interface IUiAcErrorFixer {
        void Run(AcCommonObject acObject, AcError error);
    }
}