using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools {
    public class AppRestartHelper : FatalErrorMessage.IAppRestartHelper {
        void FatalErrorMessage.IAppRestartHelper.Restart() {
            WindowsHelper.RestartCurrentApplication();
        }
    }
}