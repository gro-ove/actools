using System.IO;
using System.Windows;
using AcManager.Controls;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools {
    public static class DataUpdateWarning {
        public static bool Warn(CarObject car) {
            if (ModernDialog.ShowMessage("Are you sure you want to modify car’s data? You won’t be able to use it online with players using original version!",
                    ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;

            var data = Path.Combine(car.Location, "data.acd");
            var backup = Path.Combine(car.Location, "data_backup_cm.acd");
            if (File.Exists(data) && !File.Exists(backup)) {
                File.Copy(data, backup);
            }

            return true;
        }
    }

    public class GameWrapperUiFactory : IAnyFactory<IGameUi>, IAnyFactory<IBookingUi> {
        IGameUi IAnyFactory<IGameUi>.Create() => new GameDialog();

        IBookingUi IAnyFactory<IBookingUi>.Create() => new BookingDialog();
    }
}