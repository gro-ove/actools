using AcManager.Pages.Dialogs;

namespace AcManager.Tools.AcErrors {
    public class AcErrorFixer : IAcErrorFixer {
        void IAcErrorFixer.Run(AcError error) {
            new AcErrorSolutionSelector(error).ShowDialog();
        }
    }
}