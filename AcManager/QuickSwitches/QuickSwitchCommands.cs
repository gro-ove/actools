using System.Windows.Input;
using AcManager.Pages.Drive;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.QuickSwitches {
    public static class QuickSwitchCommands {
        public static ICommand GoCommand = new ProperAsyncCommand(o => QuickDrive.RunAsync());
    }
}