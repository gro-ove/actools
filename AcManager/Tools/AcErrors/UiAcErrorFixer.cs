using AcManager.Pages.Dialogs;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public class UiAcErrorFixer : IUiAcErrorFixer { 

        void IUiAcErrorFixer.Run(AcObjectNew acObject, AcError error) {
            new AcErrorSolutionSelector(acObject, error).ShowDialog();
        }
    }
}