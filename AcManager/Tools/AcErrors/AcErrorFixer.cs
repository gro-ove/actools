using AcManager.Pages.Dialogs;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors {
    public class AcErrorFixer : IAcErrorFixer {
        async void IAcErrorFixer.Run(AcError error) {
            AcErrorSolutionSelector selector;
            using (WaitingDialog.Create("Looking for solutions…")) {
                selector = await AcErrorSolutionSelector.Create(error);
            }

            selector.ShowDialog();
        }
    }
}