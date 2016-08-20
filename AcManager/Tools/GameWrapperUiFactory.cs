using AcManager.Pages.Dialogs;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools {
    public class GameWrapperUiFactory : IAnyFactory<IGameUi>, IAnyFactory<IBookingUi> {
        IGameUi IAnyFactory<IGameUi>.Create() => new GameDialog();

        IBookingUi IAnyFactory<IBookingUi>.Create() => new BookingDialog();
    }
}