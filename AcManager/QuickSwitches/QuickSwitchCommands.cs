using System.Windows.Input;
using AcManager.Pages.Drive;
using FirstFloor.ModernUI.Commands;

namespace AcManager.QuickSwitches {
    public static class QuickSwitchCommands {
        public static ICommand GoCommand = new AsyncCommand(() => QuickDrive.RunAsync());
    }
}